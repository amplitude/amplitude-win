using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.Web.Http;

namespace AmplitudeSDK
{
    using InstanceArgs = Tuple<Application, string>;

    public sealed class Amplitude
    {
        #region constants
        private const string LIBRARY = @"amplitude-win";
        private const string VERSION = @"2.2.4";
        private const string EVENT_LOG_URL = @"https://api.amplitude.com/";
        private const int API_VERSION = 2;
        private const int EVENT_UPLOAD_THRESHOLD = 30;
        private const int EVENT_UPLOAD_MAX_BATCH_SIZE = 100;
        private const int EVENT_MAX_COUNT = 1000;
        private const int EVENT_REMOVE_BATCH_SIZE = 20;
        private const int EVENT_UPLOAD_PERIOD_MILLISECONDS = 5000; // 30s
        private const long MIN_TIME_BETWEEN_SESSIONS_MILLIS = 15 * 1000; // 15s
        private const long SESSION_TIMEOUT_MILLIS = 30 * 60 * 1000; // 30m
        private const string SETTINGS_CONTAINER = "com.amplitude";
        private const string SETTINGS_KEY_DEVICE_ID = "deviceId";
        private const string SETTINGS_KEY_USER_ID = "userId";
        private const string SETTINGS_KEY_END_SESSION_EVENT_ID = "endSessionEventId";
        private const string SETTINGS_KEY_LAST_EVENT_TIME = "lastEventTime";
        private const string SETTINGS_KEY_PREVIOUS_SESSION_ID = "previousSessionId";
        private const string START_SESSION_EVENT = "start_session";
        private const string END_SESSION_EVENT = "end_session";
        #endregion
        private static Amplitude instance;

        private string apiKey;
        private string userId;
        private string deviceId;
        private DeviceInfo deviceInfo;
        private long sessionId;
        private Dictionary<string, object> userProperties;

        private int isUploading = 0;
        private int isUpdateScheduled = 0;
        private DatabaseHelper dbHelper;
        private Settings settings;
        private bool sessionOpen = false;

        private TaskFactory httpQueue;
        private TaskFactory logQueue;
        private Application application;

        public static Amplitude Instance
        {
            get {
                return instance;
            }
        }

        public static Amplitude Initialize(Application application, string apiKey)
        {
            return Initialize(application, apiKey, null);
        }

        public static Amplitude Initialize(Application application, string apiKey, string userId)
        {
            if (instance == null)
            {
                instance = new Amplitude(application, apiKey, userId);
            }
            return instance;
        }

        public Amplitude(Application application, string apiKey)
        {
            Init(application, apiKey, null);
        }

        public Amplitude(Application application, string apiKey, string userId)
        {
            Init(application, apiKey, userId);
        }

        private void Init(Application application, string apiKey, string userId)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("apiKey must not be null or empty");
            }
            this.application = application;
            this.httpQueue = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(1));
            this.logQueue = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(1));
            this.dbHelper = new DatabaseHelper();
            this.settings = new Settings(SETTINGS_CONTAINER);
            this.application.Resuming += new EventHandler<Object>(StartSession);
            this.application.Suspending += new SuspendingEventHandler(EndSession);
            this.apiKey = apiKey;
            if (userId != null)
            {
                this.SetUserId(userId);
            }
            else
            {
                this.userId = InitializeUserId();
            }
            this.deviceInfo = new DeviceInfo();
            this.deviceId = InitializeDeviceId();
            this.sessionId = -1;
            this.userProperties = null;
            StartSession();
        }

        public void SetUserId(string userId)
        {

            this.userId = userId;
            settings.Save(SETTINGS_KEY_USER_ID, userId);
        }

        public string InitializeUserId()
        {
            return settings.Get<string>(SETTINGS_KEY_USER_ID);
        }

        public string InitializeDeviceId()
        {
            string deviceId = settings.Get<string>(SETTINGS_KEY_DEVICE_ID);

            if (deviceId != null)
            {
                return deviceId;
            }

            // Create random device id
            string randomId = Guid.NewGuid().ToString();
            settings.Save(SETTINGS_KEY_DEVICE_ID, randomId);
            return randomId;
        }

        public string GetDeviceId(string deviceId)
        {
            return deviceId;
        }

        public void SetUserProperties(Dictionary<string, object> userProperties)
        {
            SetUserProperties(userProperties, false);
        }

        public void SetUserProperties(Dictionary<string, object> userProperties, bool replace)
        {
            if (replace)
            {
                this.userProperties = userProperties;
            }
            else
            {
                this.userProperties = Merge<string, object>(this.userProperties, userProperties);
            }
        }

        public void LogEvent(string eventType)
        {
            LogEvent(eventType, null, null, -1);
        }

        public void LogEvent(string eventType, IDictionary<string, object> eventProperties)
        {
            CheckedLogEvent(eventType, eventProperties, null, CurrentTimeMillis());
        }

        private void CheckedLogEvent(string eventType, IDictionary<string, object> eventProperties, IDictionary<string, object> apiProperties, long timestamp)
        {
            if (string.IsNullOrEmpty(eventType))
            {
                return;
            }
            logQueue.StartNew(() =>
            {
                LogEvent(eventType, eventProperties, apiProperties, timestamp);
            });
        }

        private int LogEvent(string eventType, IDictionary<string, object> eventProperties, IDictionary<string, object> apiProperties, long timestamp)
        {
            if (timestamp <= 0)
            {
                timestamp = CurrentTimeMillis();
            }

            {
                var evt = new Dictionary<string, object>();

                evt.Add("event_type", eventType);
                evt.Add("event_properties", eventProperties);
                if (apiProperties == null)
                {
                    apiProperties = new Dictionary<string, object>();
                }
                var advertiserId = deviceInfo.GetAdvertiserId();
                if (advertiserId != null)
                {
                    apiProperties.Add("wp_adid", advertiserId);
                }
                evt.Add("api_properties", apiProperties);
                evt.Add("user_properties", userProperties);
                evt.Add("timestamp", timestamp);
                evt.Add("user_id", userId);
                evt.Add("device_id", deviceId);
                evt.Add("session_id", sessionId);
                evt.Add("platform", deviceInfo.GetPlatform());
                evt.Add("version_name", deviceInfo.GetAppVersion());
                evt.Add("os_name", deviceInfo.GetOsName());
                evt.Add("os_version", deviceInfo.GetOsVersion());
                // no brand information
                // evt.Add("device_brand", deviceInfo.GetBrand());
                evt.Add("device_manufacturer", deviceInfo.GetManufacturer());
                evt.Add("device_model", deviceInfo.GetModel());
                evt.Add("carrier", deviceInfo.GetCarrier());
                evt.Add("country", deviceInfo.GetCountry());
                evt.Add("language", deviceInfo.GetLanguage());
                evt.Add("library", new Dictionary<string, object> {
                    {"name", LIBRARY},
                    {"version", VERSION}
                });
                // TODO: add location

                settings.Save(SETTINGS_KEY_LAST_EVENT_TIME, timestamp);

                int eventId = dbHelper.AddEvent(JsonConvert.SerializeObject(evt));
                if (dbHelper.GetEventCount() >= EVENT_MAX_COUNT)
                {
                    dbHelper.RemoveEvents(dbHelper.GetNthEventId(EVENT_REMOVE_BATCH_SIZE));
                }
                if (dbHelper.GetEventCount() >= EVENT_UPLOAD_THRESHOLD)
                {
                    UpdateServer(false);
                }
                else
                {
                    UpdateServerDelayed(EVENT_UPLOAD_PERIOD_MILLISECONDS);
                }
                return eventId;
            }
        }

        private IEnumerable<JObject> GetLastEvents(int maxEventId, int batchSize)
        {
            try
            {
                Tuple<int, IEnumerable<Event>> pair = dbHelper.GetEvents(maxEventId, batchSize);
                int maxId = pair.Item1;
                IEnumerable<Event> events = pair.Item2;
                return events.Select(e =>
                {
                    JObject obj = JObject.Parse(e.Text);
                    obj.Add("event_id", new JValue(e.Id));
                    return obj;
                });
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e);
            }
            return new List<JObject>();
        }

        private void UpdateServer(bool uploadRemaining)
        {
            if (0 == Interlocked.Exchange(ref isUploading, 1))
            {
                int batchSize = uploadRemaining ? -1 : EVENT_UPLOAD_MAX_BATCH_SIZE;
                IEnumerable<JObject> lastEvents = GetLastEvents(-1, batchSize);
                httpQueue.StartNew(async () =>
                {
                    bool success = await PostEvents(lastEvents);
                    if (success)
                    {
                        logQueue.StartNew(() =>
                        {
                            var maxId = (int)lastEvents.Last()["event_id"];
                            dbHelper.RemoveEvents(maxId);
                            isUploading = 0;
                            if (dbHelper.GetEventCount() > EVENT_UPLOAD_THRESHOLD)
                            {
                                logQueue.StartNew(() =>
                                {
                                    UpdateServer(false);
                                });
                            }
                        });
                    }
                    else
                    {
                        isUploading = 0;
                    }
                    return success;
                });
            }
        }

        private void UpdateServerDelayed(int delayMs)
        {
            if (0 == Interlocked.Exchange(ref isUpdateScheduled, 1))
            {
                logQueue.StartNew(async () =>
                {
                    await Task.Delay(delayMs);
                    isUpdateScheduled = 0;
                    UpdateServer(false);
                    return true;
                });
            }
        }

        /// <summary>
        /// Sends events to server
        /// </summary>
        /// <param name="events"></param>
        /// <returns>true if post request was made and successful (events should be removed)</returns>
        private async Task<bool> PostEvents(IEnumerable<JObject> events)
        {
            if (events.Count() <= 0)
            {
                return false;
            }
            using (var httpClient = new HttpClient())
            {
                bool uploadSuccess = false;
                string version = API_VERSION.ToString();
                string uploadTime = CurrentTimeMillis().ToString();
                string eventsJson = null;
                try
                {
                    eventsJson = JsonConvert.SerializeObject(events);
                }
                catch (JsonException e)
                {
                    Debug.WriteLine("Error serializing events");
                    Debug.WriteLine(e);
                    return false;
                }
                string checksum = Hash(version + apiKey + eventsJson + uploadTime);
                Debug.WriteLine(checksum);
                Debug.WriteLine(version + apiKey + eventsJson + uploadTime);
                var postParams = new Dictionary<string, string>();
                postParams.Add("v", API_VERSION.ToString());
                postParams.Add("client", apiKey);
                postParams.Add("e", eventsJson);
                postParams.Add("upload_time", uploadTime);
                postParams.Add("checksum", checksum);
                var content = new HttpFormUrlEncodedContent(postParams);
                var uri = new Uri(EVENT_LOG_URL);
                HttpResponseMessage result = await httpClient.PostAsync(uri, content);
                string response = await result.Content.ReadAsStringAsync();
                Debug.WriteLine(response);
                if (response == "success")
                {
                    uploadSuccess = true;
                }
                else if (response == "invalid_api_key")
                {
                    Debug.WriteLine("Invalid API key, make sure your API key is correct in initialize()");
                }
                else if (response == "bad_checksum")
                {
                    Debug.WriteLine("Bad checksum, post request was mangled in transit, will attempt to reupload later");
                }
                else if (response == "request_db_write_failed")
                {
                    Debug.WriteLine("Couldn't write to request database on server, will attempt to reupload later");
                }
                else
                {
                    Debug.WriteLine("Upload failed, " + response + ", will attempt to reupload later");
                }
                return uploadSuccess;

            }
        }

        private void LogStartSession(long timestamp)
        {
            Dictionary<string, object> apiProperties = new Dictionary<string, object>();
            apiProperties.Add("special", START_SESSION_EVENT);
            LogEvent(START_SESSION_EVENT, null, apiProperties, timestamp);
        }

        private void LogEndSession(long timestamp)
        {
            Dictionary<string, object> apiProperties = new Dictionary<string, object>();
            apiProperties.Add("special", END_SESSION_EVENT);
            LogEvent(END_SESSION_EVENT, null, apiProperties, timestamp);
        }

        private void StartSession(Object sender, Object e)
        {
            StartSession();
            Debug.WriteLine("Start Session");
        }

        private void StartNewSession(long timestamp)
        {
            sessionOpen = true;
            sessionId = timestamp;
            settings.Save(SETTINGS_KEY_PREVIOUS_SESSION_ID, sessionId);
            LogStartSession(timestamp);
        }

        public void StartSession()
        {
            long timestamp = CurrentTimeMillis();
            logQueue.StartNew(() =>
            {
                if (!sessionOpen)
                {
                    long lastEventTime = settings.Get<long>(SETTINGS_KEY_LAST_EVENT_TIME);
                    if (timestamp - lastEventTime < MIN_TIME_BETWEEN_SESSIONS_MILLIS)
                    {
                        long previousSessionId = settings.Get<long>(SETTINGS_KEY_PREVIOUS_SESSION_ID);
                        if (previousSessionId <= 0)
                        {
                            // Invalid session id, create new session
                            StartNewSession(timestamp);
                        }
                        else
                        {
                            sessionId = previousSessionId;
                            LogStartSession(timestamp);
                        }
                    }
                    else
                    {
                        // Start a new session if previous end session was more than MIN_TIME_BETWEEN_SESSIONS_MILLIS of now
                        StartNewSession(timestamp);
                    }

                }
                else
                {
                    // Start a new session if last event was more than SESSION_TIMEOUT_MILLIS ago
                    long lastEventTime = settings.Get<long>(SETTINGS_KEY_LAST_EVENT_TIME);
                    if (timestamp - lastEventTime > SESSION_TIMEOUT_MILLIS || sessionId <= 0)
                    {
                        StartNewSession(timestamp);
                    }
                }
                sessionOpen = true;
            });
        }

        private void EndSession(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            long timestamp = CurrentTimeMillis();
            logQueue.StartNew(() =>
            {
                if (sessionOpen)
                {
                    // only log end session event if a session is open
                    LogEndSession(timestamp);
                    Debug.WriteLine("End Session");
                }
                sessionOpen = false;
                UpdateServer(false);
            });
        }

        private long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        private string Hash(string data)
        {
            IBuffer utf8buff = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);
            IBuffer hash = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5).HashData(utf8buff);
            return CryptographicBuffer.EncodeToHexString(hash);
        }

        private Dictionary<K, V> Merge<K, V>(Dictionary<K, V> a, Dictionary<K, V> b)
        {
            if (a == null)
            {
                return b;
            }
            if (b == null)
            {
                return a;
            }
            return b.Concat(a.Where(kvp => !b.ContainsKey(kvp.Key))).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    class Settings
    {
        ApplicationDataContainer container;

        internal Settings(string container)
        {
            this.container = ApplicationData.Current.LocalSettings.CreateContainer(container, ApplicationDataCreateDisposition.Always);
        }

        internal T Get<T>(string key)
        {
            T value = default(T);
            try
            {
                value = (T)container.Values[key];
            }
            catch
            {
                // Error casting
            }
            return value;
        }

        internal void Save(string key, Object value)
        {
            container.Values[key] = value;
        }
    }
}
