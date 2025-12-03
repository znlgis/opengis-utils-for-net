# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
  - NetTopologySuite 2.5.0
  - NetTopologySuite.IO.GeoJSON 4.0.0
  - NetTopologySuite.IO.ShapeFile 2.1.0
  - MaxRev.Gdal.Core 3.9.2.259
  - MaxRev.Gdal.Universal 3.9.2.259
  - System.Text.Json 8.0.5
  - System.Text.Encoding.CodePages 7.0.0
  - Microsoft.Extensions.Logging.Abstractions 7.0.0
  - SharpZipLib 1.4.2

## [1.0.0] - TBD

### Added
- First stable release

[Unreleased]: https://github.com/znlgis/opengis-utils-for-net/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/znlgis/opengis-utils-for-net/releases/tag/v1.0.0
