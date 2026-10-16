# HeatMapTracker

A .NET MAUI application that records the user's location, persists each fix to a local
SQLite database, and visualises the accumulated history as a heat map drawn over the
.NET MAUI `Map` control.

## What it does

1. Polls the platform geolocation provider every 10 seconds while tracking is enabled.
2. Discards any fix less than 15 metres from the previously stored one, so a stationary
   device does not flood the database.
3. Writes each accepted fix to SQLite (`locations.db3` in the app data directory).
4. Aggregates all stored fixes into a 50-metre grid and draws one translucent circle per
   occupied cell, coloured from blue through green to red by visit density.

## Why a grid rather than one circle per point

The stock MAUI `Map` has no heat map primitive. Drawing a circle for every raw fix would
produce hundreds of overlapping shapes and no sense of density. `HeatMapRenderer` instead
snaps fixes to a fixed grid, counts visits per cell, and renders one circle per cell with
colour and opacity driven by that count. This gives the cold-to-hot appearance of a real
heat map using only documented MAUI APIs, with no custom platform renderer required.

Longitude spacing is divided by the cosine of the latitude so that cells remain
approximately square as the map moves away from the equator. Verified at 0°, 37° and 60°
latitude, cells measure 49.9 m on both axes.

## Project layout

| Path | Purpose |
|---|---|
| `Models/LocationPoint.cs` | SQLite entity for one recorded fix |
| `Models/HeatCell.cs` | Aggregated grid cell with a visit count |
| `Services/LocationDatabase.cs` | Async SQLite data access |
| `Services/LocationTrackingService.cs` | Permission handling and the polling loop |
| `Services/HeatMapRenderer.cs` | Grid aggregation and circle rendering |
| `Views/MainView.xaml` | Map, status line, and the two control buttons |
| `Platforms/Android/AndroidManifest.xml` | Location permissions and the Maps API key placeholder |
| `Platforms/Android/MainActivity.cs` | Android entry point |
| `maps.props.example` | Template for the untracked file holding the Maps API key |
| `Platforms/iOS/Info.plist` | Location usage descriptions |

## Prerequisites

- .NET 10 SDK
- .NET MAUI Android workload: `dotnet workload install maui-android`
- Android SDK (API 36) and a JDK 21 installation
- A Google Maps API key (see below)

Only `net10.0-android` is built by default. Adding `net10.0-ios` requires a full Xcode
installation; the command line tools alone are not sufficient.

## Google Maps API key

Android map rendering requires a key, and without one the map draws no tiles and the
log reports `Authorization failure`. The key is injected at build time rather than
committed, so it stays out of the repository.

1. In the Google Cloud console, enable **Maps SDK for Android**.
2. Create an API key and restrict it to this application:
   - Application id: `com.ucumberlands.heatmaptracker`
   - Debug signing certificate SHA-1 fingerprint, obtained with:

     ```bash
     keytool -list -v -storepass android \
       -keystore "$HOME/Library/Application Support/Xamarin/Mono for Android/debug.keystore"
     ```

3. Copy the key into a local file that git ignores:

   ```bash
   cp maps.props.example maps.props   # then paste the key into maps.props
   ```

`maps.props` supplies the `MapsApiKey` property, which the build substitutes into the
`${MAPS_API_KEY}` placeholder in `Platforms/Android/AndroidManifest.xml`.

## Build and run

```bash
dotnet restore
dotnet build -f net10.0-android
dotnet build -t:Run -f net10.0-android      # deploy to a connected device or emulator
```

Grant the location permission when prompted, press **Start Tracking**, and move around.
Circles appear as fixes accumulate and warm from blue toward red where you spend time.

On an emulator, drive the location from Extended Controls → Location, or from the
command line:

```bash
adb emu geo fix -96.7970 32.7767            # longitude first, then latitude
```

## Coding conventions

Written against the Microsoft C# coding conventions: PascalCase for types and members,
`_camelCase` for private fields, explicit access modifiers, file-scoped namespaces,
nullable reference types enabled, and XML documentation comments on all public members.
