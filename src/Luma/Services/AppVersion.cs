using System;
namespace Luma.Services;
internal static class AppVersion { public static Version Current => typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0); }
