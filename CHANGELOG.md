# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `OpenGIS.Utils.RealDataHarness`: reusable console harness plus `RealDataChecks`
  integration checks that auto-discover Shapefiles under a caller-supplied
  directory (recursive `.shp`/`.dbf` pairing, expected feature counts parsed
  from DBF headers and geometry types from SHP headers independently of GDAL)
  across six dimensions (read, 3D geometry, CRS, format conversion, geometry
  operations, write round trip); no dataset names, paths, or coordinate-range
  assumptions are built in. `RealDataIntegrationTests` reuses the checks in
  xunit via the `OGU_REAL_DATA_DIR` environment variable (auto-skipped when
  unset).
- `OguLogging`: configurable library-wide logging facade based on the
  `Microsoft.Extensions.Logging.Abstractions` dependency. Defaults to no output
  (`NullLoggerFactory`); set `OguLogging.LoggerFactory` at startup to receive
  internal diagnostics that were previously silently discarded.
- `GtTxtUtil.TryParseTxtLine`: non-throwing coordinate-line parsing API while
  retaining the nullable `ParseTxtLine` method for compatibility.
- `PostgisUtil.CreateSpatialIndex`: GIST index creation through the GDAL
  PostgreSQL driver with safe identifier validation.
- Initial project structure and solution setup
- Core enums: GeometryType, FieldDataType, GisEngineType, DataFormatType, TopologyValidationErrorType
- Exception system: OguException, DataSourceException, FormatParseException, EngineNotSupportedException, LayerValidationException, TopologyException
- Core model classes:
  - OguLayer: Unified GIS layer definition with validation and serialization
  - OguFeature: Feature class with attribute management
  - OguField: Field definition with data type support
  - OguFieldValue: Type-safe field value container
  - OguCoordinate: 2D/3D coordinate support with point/ring number for TXT format
  - OguLayerMetadata: Layer metadata container
  - TopologyValidationResult: Topology validation result
  - SimpleGeometryResult: Simple geometry check result
  - DbConnBaseModel: Database connection model
  - GdbGroupModel: FileGDB group model
- NuGet dependencies:
  - MaxRev.Gdal.Core 3.13.3.557
  - MaxRev.Gdal.Universal 3.13.3.557
  - System.Text.Json 10.0.11
  - System.Text.Encoding.CodePages 10.0.11
  - Microsoft.Extensions.Logging.Abstractions 10.0.11
  - SharpZipLib 1.4.2
  - System.Memory 4.6.3
  - System.Buffers 4.6.1

### Changed
- `GdalWriter` now resolves the layer geometry type from the first parseable
  feature WKT, upgrading PointZ/PolylineZ layers to their 25D OGR type so GPKG
  no longer reports a 2D-declared layer containing Z geometries.
- `GdalWriter` maps the `.gdb` extension to the `OpenFileGDB` driver (writable
  since GDAL 3.6) instead of the ESRI SDK-dependent `FileGDB` driver.
- `GdalWriter` writes null attribute values with `SetFieldNull` (falling back
  to `UnsetField`) and enables `WRITE_NULL_FIELDS=YES` for GeoJSON output so
  fields whose values are all null survive SHP→GeoJSON→SHP round trips.
- `GdalWriter` skips field creation on fixed-schema drivers (DXF) with a
  warning instead of failing the whole write.
- `GtTxtUtil.SaveTxt` fills missing 点号 with the feature Fid and missing 圈号
  with "1" so saved files can be re-read by `LoadTxt`.
- Replaced `Console.WriteLine` diagnostics in `GdalWriter` with structured logging.
- Silent `catch` blocks in `GdalReader`, `GdalWriter`, `PostgisUtil`, and
  `GtTxtUtil` now log the swallowed exception (Debug/Warning) instead of
  discarding it, preserving the previous control flow.
- `GdalReader` documents and protects the process-wide encoding setting used
  for encoded Shapefile reads; `GetLayerNames` uses the same configuration
  guard.
- `GdalWriter.Write` and `Append` document their recreation, append, field
  mapping, FID, and failed-feature behavior.
- `ShpUtil.GetShapefileBounds` now documents typed failures for unreadable
  extents and the valid empty-Shapefile result.

### Fixed
- `GdalReader` returns null for OGR null fields (e.g. blank Shapefile `D`
  date fields) instead of throwing `FormatParseException` on the first empty
  date, which previously made layers with empty date fields unreadable.
- `GdalWriter.Write`/`Append` retry once with an auto-assigned FID when an
  explicit source FID collides with the driver-assigned FID (GPKG/OpenFileGDB
  UNIQUE constraint), instead of dropping the feature.
- `GdalConfiguration` now locates the MaxRev-deployed
  `runtimes/any/native/gdal-data` directory and points `GDAL_DATA` at it,
  restoring DXF write support (template `header.dxf`).
- `GtTxtUtil.LoadTxt` skips the column header line written by `SaveTxt`, so
  SaveTxt→LoadTxt round trips succeed.
- `GeometryUtil.Envelope` no longer leaks the temporary linear-ring native
  geometry after it is cloned into the result polygon.
- Invalid TXT coordinate rows, invalid GDAL date fields, and failed GDAL
  field conversions are no longer silently lost or reported as untyped errors.
- Geometry-to-WKT export results and Shapefile extent return codes are checked
  before returning data to callers.

## [1.0.0] - TBD

### Added
- First stable release

[Unreleased]: https://github.com/znlgis/opengis-utils-for-net/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/znlgis/opengis-utils-for-net/releases/tag/v1.0.0
