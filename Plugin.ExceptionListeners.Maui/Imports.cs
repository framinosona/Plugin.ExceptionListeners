// Platform-specific namespaces (conditionally compiled)
#if ANDROID
global using Android.OS;
global using Android.Runtime;
#endif

#if IOS || MACCATALYST
global using ObjCRuntime;
#endif

#if WINDOWS
global using Windows.ApplicationModel.Core;
#endif
