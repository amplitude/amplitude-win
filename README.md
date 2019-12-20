# THIS REPO IS ARCHIVED. AMPLITUDE NO LONGER SUPPORT WINDOWS APP SDK. #

# Setup #
1. If you haven't already, go to https://amplitude.com/signup and register for an account. Then, add an app. You will receive an API Key.

2. In every file that uses analytics, add the `AmplitudeSDK` namespace at the top:

    ```c#
    using AmplitudeSDK;
    ```

5. In the constructor of your App, initialize the SDK:

    ```c#
    Amplitude.Initialize(this, "YOUR_API_KEY_HERE");
    ```

6. To track an event anywhere in the app, call:

    ```c#
    Amplitude.Instance.LogEvent("EVENT_IDENTIFIER_HERE");
    ```

9. Events are saved locally. Uploads are batched to occur every 30 events and every 30 seconds. After calling `LogEvent()` in your app, you will immediately see data appear on the Amplitude website.

# Tracking Events #

It's important to think about what types of events you care about as a developer. You should aim to track between 20 and 100 types of events within your app. Common event types are different screens within the app, actions a user initiates (such as pressing a button), and events you want a user to complete (such as filling out a form, completing a level, or making a payment). Contact us if you want assistance determining what would be best for you to track.

# Tracking Sessions #

A session is a period of time that a user has the app in the foreground. Sessions within 15 seconds of each other are merged into a single session. In the Windows SDK, sessions are tracked automatically.

# Setting Custom User IDs #

If your app has its own login system that you want to track users with, you can call `SetUserId()` at any time:

```c#
Amplitude.Instance.SetUserId("USER_ID_HERE");
```

A user's data will be merged on the backend so that any events up to that point on the same device will be tracked under the same user.

You can also add a user ID as an argument to the `Initialize()` call:

```c#
Amplitude.Initialize(this, "YOUR_API_KEY_HERE", "USER_ID_HERE");
```

# Setting Event Properties #

You can attach additional data to any event by passing a `Dictionary<string, object>` as the second argument to `LogEvent()`:

```c#
Dictionary<string, object> eventProperties = new Dictionary<string, object>()
{
    {"KEY", "VALUE"}
}
Amplitude.Instance.LogEvent("Sent Message", eventProperties);
```

# Setting User Properties #

To add properties that are associated with a user, you can set user properties:

```c#
Dictionary<string, object> userProperties = new Dictionary<string, object>()
{
    {"KEY", "VALUE"}
}
Amplitude.Instance.SetUserProperties(userProperties);
```

# Advanced #

If you want to use the source files directly, you can [download them here](https://github.com/amplitude/amplitude-win/archive/master.zip). To include them in your project, extract the files, and then copy the five *.java files into your Android project.

This SDK automatically grabs useful data from the phone, including app version, phone model, operating system version, and language information. 

By default, device IDs are a randomly generated UUID. You can retrieve the Device ID that Amplitude uses with `Amplitude.Instance.GetDeviceId()`. This method can return null if a Device ID hasn't been generated yet.
