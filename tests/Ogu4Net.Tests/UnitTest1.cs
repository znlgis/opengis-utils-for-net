using Ogu4Net.Common;
using Ogu4Net.Enums;
using Ogu4Net.Geometry;
using Ogu4Net.Model;
using Ogu4Net.Model.Layer;
using System.Collections.Generic;
using Xunit;

namespace Ogu4Net.Tests
{
    public class GeometryConverterTests
    {
        [Fact]
        public void Wkt2Geometry_Point_ReturnsPoint()
        {
            var wkt = "POINT (100 200)";
            var geom = GeometryConverter.Wkt2Geometry(wkt);
            Assert.NotNull(geom);
            Assert.Equal("Point", geom.GeometryType);
        }

        [Fact]
        public void Wkt2Geometry_Polygon_ReturnsPolygon()
        {
            var wkt = "POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))";
            var geom = GeometryConverter.Wkt2Geometry(wkt);
            Assert.NotNull(geom);
            Assert.Equal("Polygon", geom.GeometryType);
        }

        [Fact]
        public void Geometry2Wkt_RoundTrip_Success()
        {
            var originalWkt = "POINT (100 200)";
            var geom = GeometryConverter.Wkt2Geometry(originalWkt);
            var resultWkt = GeometryConverter.Geometry2Wkt(geom);
            Assert.Contains("100", resultWkt);
            Assert.Contains("200", resultWkt);
        }

        [Fact]
        public void Wkt2GeoJson_ReturnsValidGeoJson()
        {
            var wkt = "POINT (100 200)";
            var geoJson = GeometryConverter.Wkt2GeoJson(wkt);
            Assert.NotNull(geoJson);
            Assert.Contains("Point", geoJson);
            Assert.Contains("coordinates", geoJson);
        }

        [Fact]
        public void GeoJson2Wkt_ReturnsValidWkt()
        {
            var geoJson = "{\"type\":\"Point\",\"coordinates\":[100,200]}";
            var wkt = GeometryConverter.GeoJson2Wkt(geoJson);
            Assert.NotNull(wkt);
            Assert.Contains("POINT", wkt.ToUpper());
        }

        [Fact]
        public void TryParseWkt_ValidWkt_ReturnsTrue()
        {
            var wkt = "POINT (100 200)";
            var result = GeometryConverter.TryParseWkt(wkt, out var geom);
            Assert.True(result);
            Assert.NotNull(geom);
        }

        [Fact]
        public void TryParseWkt_InvalidWkt_ReturnsFalse()
        {
            var wkt = "INVALID";
            var result = GeometryConverter.TryParseWkt(wkt, out var geom);
            Assert.False(result);
            Assert.Null(geom);
        }
    }

    public class NtsGeometryUtilTests
    {
        [Fact]
        public void IsEmpty_EmptyGeometry_ReturnsTrue()
        {
            var geom = GeometryConverter.Wkt2Geometry("POINT EMPTY");
            Assert.True(NtsGeometryUtil.IsEmpty(geom));
        }

        [Fact]
        public void Area_Polygon_ReturnsCorrectArea()
        {
            var geom = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            var area = NtsGeometryUtil.Area(geom);
            Assert.Equal(100, area);
        }

        [Fact]
        public void Length_LineString_ReturnsCorrectLength()
        {
            var geom = GeometryConverter.Wkt2Geometry("LINESTRING (0 0, 10 0)");
            var length = NtsGeometryUtil.Length(geom);
            Assert.Equal(10, length);
        }

        [Fact]
        public void Buffer_Point_ReturnsPolygon()
        {
            var geom = GeometryConverter.Wkt2Geometry("POINT (0 0)");
            var buffered = NtsGeometryUtil.Buffer(geom, 10);
            Assert.Equal("Polygon", buffered.GeometryType);
        }

        [Fact]
        public void Intersects_OverlappingPolygons_ReturnsTrue()
        {
            var geomA = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            var geomB = GeometryConverter.Wkt2Geometry("POLYGON ((5 5, 15 5, 15 15, 5 15, 5 5))");
            Assert.True(NtsGeometryUtil.Intersects(geomA, geomB));
        }

        [Fact]
        public void Disjoint_NonOverlappingPolygons_ReturnsTrue()
        {
            var geomA = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            var geomB = GeometryConverter.Wkt2Geometry("POLYGON ((20 20, 30 20, 30 30, 20 30, 20 20))");
            Assert.True(NtsGeometryUtil.Disjoint(geomA, geomB));
        }

        [Fact]
        public void Contains_InnerPolygon_ReturnsTrue()
        {
            var outer = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 20 0, 20 20, 0 20, 0 0))");
            var inner = GeometryConverter.Wkt2Geometry("POLYGON ((5 5, 15 5, 15 15, 5 15, 5 5))");
            Assert.True(NtsGeometryUtil.Contains(outer, inner));
        }

        [Fact]
        public void IsValid_ValidPolygon_ReturnsTrue()
        {
            var geom = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            var result = NtsGeometryUtil.IsValid(geom);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void GetGeometryType_Polygon_ReturnsPolygon()
        {
            var geom = GeometryConverter.Wkt2Geometry("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))");
            var type = NtsGeometryUtil.GetGeometryType(geom);
            Assert.Equal(GeometryType.Polygon, type);
        }

        [Fact]
        public void ConvexHull_Points_ReturnsPolygon()
        {
            var geom = GeometryConverter.Wkt2Geometry("MULTIPOINT ((0 0), (10 0), (5 10), (0 10), (10 10))");
            var hull = NtsGeometryUtil.ConvexHull(geom);
            Assert.NotNull(hull);
            Assert.True(hull.IsValid);
        }

        [Fact]
        public void Simplify_ComplexLine_ReducesPoints()
        {
            var geom = GeometryConverter.Wkt2Geometry("LINESTRING (0 0, 1 0.1, 2 0, 3 0.1, 4 0, 5 0.1, 6 0)");
            var simplified = NtsGeometryUtil.Simplify(geom, 0.5);
            Assert.True(simplified.NumPoints <= geom.NumPoints);
        }
    }

    public class OguLayerTests
    {
        [Fact]
        public void FromJson_ValidJson_ReturnsLayer()
        {
            var json = "{\"Name\":\"TestLayer\",\"Wkid\":4490,\"GeometryType\":5}";
            var layer = OguLayer.FromJson(json);
            Assert.NotNull(layer);
            Assert.Equal("TestLayer", layer.Name);
            Assert.Equal(4490, layer.Wkid);
        }

        [Fact]
        public void ToJson_Layer_ReturnsValidJson()
        {
            var layer = new OguLayer
            {
                Name = "TestLayer",
                Wkid = 4490,
                GeometryType = GeometryType.Polygon
            };
            var json = layer.ToJson();
            Assert.NotNull(json);
            Assert.Contains("TestLayer", json);
        }

        [Fact]
        public void Filter_ByAttribute_ReturnsMatchingFeatures()
        {
            var layer = new OguLayer
            {
                Name = "TestLayer",
                Wkid = 4490,
                GeometryType = GeometryType.Point,
                Fields = new List<OguField>
                {
                    new OguField("city", "城市", FieldDataType.String)
                },
                Features = new List<OguFeature>
                {
                    new OguFeature("1", "POINT (116 39)", new List<OguFieldValue>
                    {
                        new OguFieldValue(new OguField("city", "城市", FieldDataType.String), "北京")
                    }),
                    new OguFeature("2", "POINT (121 31)", new List<OguFieldValue>
                    {
                        new OguFieldValue(new OguField("city", "城市", FieldDataType.String), "上海")
                    })
                }
            };

            var filtered = layer.Filter((Func<OguFeature, bool>)(f => "北京".Equals(f.GetValue("city"))));
            Assert.Single(filtered);
            Assert.Equal("1", filtered[0].Id);
        }

        [Fact]
        public void GetFeatureCount_ReturnsCorrectCount()
        {
            var layer = new OguLayer
            {
                Features = new List<OguFeature>
                {
                    new OguFeature(),
                    new OguFeature(),
                    new OguFeature()
                }
            };
            Assert.Equal(3, layer.GetFeatureCount());
        }

        [Fact]
        public void Validate_MissingName_ThrowsException()
        {
            var layer = new OguLayer
            {
                Wkid = 4490,
                GeometryType = GeometryType.Point
            };
            Assert.Throws<System.InvalidOperationException>(() => layer.Validate());
        }
    }

    public class OguFeatureTests
    {
        [Fact]
        public void GetValue_ExistingField_ReturnsValue()
        {
            var feature = new OguFeature
            {
                Id = "1",
                Geometry = "POINT (0 0)",
                Attributes = new List<OguFieldValue>
                {
                    new OguFieldValue(new OguField("name", null, FieldDataType.String), "Test")
                }
            };

            var value = feature.GetValue("name");
            Assert.Equal("Test", value);
        }

        [Fact]
        public void GetValue_NonExistingField_ReturnsNull()
        {
            var feature = new OguFeature { Attributes = new List<OguFieldValue>() };
            var value = feature.GetValue("nonexistent");
            Assert.Null(value);
        }

        [Fact]
        public void SetValue_ExistingField_UpdatesValue()
        {
            var feature = new OguFeature
            {
                Attributes = new List<OguFieldValue>
                {
                    new OguFieldValue(new OguField("name", null, FieldDataType.String), "Old")
                }
            };

            var result = feature.SetValue("name", "New");
            Assert.True(result);
            Assert.Equal("New", feature.GetValue("name"));
        }
    }

    public class OguFieldValueTests
    {
        [Fact]
        public void GetIntValue_ValidInt_ReturnsInt()
        {
            var fieldValue = new OguFieldValue(new OguField("count", null, FieldDataType.Integer), 42);
            Assert.Equal(42, fieldValue.GetIntValue());
        }

        [Fact]
        public void GetDoubleValue_ValidDouble_ReturnsDouble()
        {
            var fieldValue = new OguFieldValue(new OguField("amount", null, FieldDataType.Double), 3.14);
            Assert.Equal(3.14, fieldValue.GetDoubleValue());
        }

        [Fact]
        public void GetStringValue_AnyValue_ReturnsString()
        {
            var fieldValue = new OguFieldValue(new OguField("value", null, FieldDataType.Integer), 42);
            Assert.Equal("42", fieldValue.GetStringValue());
        }
    }

    public class SortUtilTests
    {
        [Fact]
        public void CompareString_Numbers_SortsNaturally()
        {
            Assert.True(SortUtil.CompareString("第5章", "第10章") < 0);
            Assert.True(SortUtil.CompareString("第10章", "第5章") > 0);
        }

        [Fact]
        public void CompareString_SameStrings_ReturnsZero()
        {
            Assert.Equal(0, SortUtil.CompareString("test", "test"));
        }

        [Fact]
        public void CompareString_MixedContent_SortsCorrectly()
        {
            Assert.True(SortUtil.CompareString("file1.txt", "file2.txt") < 0);
            Assert.True(SortUtil.CompareString("file10.txt", "file2.txt") > 0);
        }
    }

    public class NumUtilTests
    {
        [Fact]
        public void GetPlainString_ScientificNotation_ReturnsPlainString()
        {
            var result = NumUtil.GetPlainString(1.234E10);
            Assert.DoesNotContain("E", result);
            Assert.Contains("12340000000", result);
        }

        [Fact]
        public void GetPlainString_SmallDecimal_PreservesDigits()
        {
            var result = NumUtil.GetPlainString(0.000001);
            Assert.Contains("0.000001", result);
        }
    }

    public class CrsUtilTests
    {
        [Fact]
        public void GetTolerance_Geographic_ReturnsSmallValue()
        {
            var tolerance = CrsUtil.GetTolerance(4490);
            Assert.True(tolerance < 0.0001);
        }

        [Fact]
        public void GetTolerance_Projected_ReturnsLargerValue()
        {
            var tolerance = CrsUtil.GetTolerance(4527);
            Assert.Equal(0.0001, tolerance);
        }

        [Fact]
        public void IsProjectedCrs_GeographicWkid_ReturnsFalse()
        {
            Assert.False(CrsUtil.IsProjectedCrs(4490));
        }

        [Fact]
        public void IsProjectedCrs_ProjectedWkid_ReturnsTrue()
        {
            Assert.True(CrsUtil.IsProjectedCrs(4527));
        }

        [Fact]
        public void GetZoneNumber_ByWkid_ReturnsCorrectZone()
        {
            Assert.Equal(39, CrsUtil.GetZoneNumberFromWkid(4527));
        }

        [Fact]
        public void GetProjectedWkid_FromZone_ReturnsCorrectWkid()
        {
            Assert.Equal(4527, CrsUtil.GetProjectedWkid(39));
        }
    }

    public class GeometryTypeTests
    {
        [Fact]
        public void FromTypeName_Point_ReturnsPointType()
        {
            var type = GeometryTypeExtensions.FromTypeName("Point");
            Assert.Equal(GeometryType.Point, type);
        }

        [Fact]
        public void FromTypeName_CaseInsensitive_Works()
        {
            var type = GeometryTypeExtensions.FromTypeName("POLYGON");
            Assert.Equal(GeometryType.Polygon, type);
        }

        [Fact]
        public void GetNtsType_Point_ReturnsPointClass()
        {
            var type = GeometryType.Point.GetNtsType();
            Assert.Equal(typeof(NetTopologySuite.Geometries.Point), type);
        }
    }
}
