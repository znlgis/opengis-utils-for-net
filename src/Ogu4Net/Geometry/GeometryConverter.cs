using System;
using System.IO;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace Ogu4Net.Geometry
{
    /// <summary>
    /// 几何格式转换工具类
    /// <para>
    /// 提供NTS Geometry与各种几何格式（WKT、GeoJSON）之间的相互转换功能。
    /// 所有方法均为静态方法，无需实例化即可使用。
    /// </para>
    /// </summary>
    public static class GeometryConverter
    {
        private static readonly WKTReader WktReader = new WKTReader();
        private static readonly WKTWriter WktWriter = new WKTWriter();
        private static readonly GeoJsonReader GeoJsonReader = new GeoJsonReader();
        private static readonly GeoJsonWriter GeoJsonWriter = new GeoJsonWriter();

        // ==================== WKT转换方法 ====================

        /// <summary>
        /// WKT转NTS Geometry
        /// </summary>
        /// <param name="wkt">WKT格式的字符串</param>
        /// <returns>NTS Geometry对象</returns>
        public static NetTopologySuite.Geometries.Geometry Wkt2Geometry(string wkt)
        {
            if (string.IsNullOrEmpty(wkt))
                throw new ArgumentNullException(nameof(wkt));

            return WktReader.Read(wkt);
        }

        /// <summary>
        /// WKT转GeoJSON
        /// </summary>
        /// <param name="wkt">WKT格式的字符串</param>
        /// <returns>GeoJSON格式的字符串</returns>
        public static string Wkt2GeoJson(string wkt)
        {
            var geometry = Wkt2Geometry(wkt);
            return Geometry2GeoJson(geometry);
        }

        // ==================== GeoJSON转换方法 ====================

        /// <summary>
        /// GeoJSON转NTS Geometry
        /// </summary>
        /// <param name="geoJson">GeoJSON格式的字符串</param>
        /// <returns>NTS Geometry对象</returns>
        public static NetTopologySuite.Geometries.Geometry GeoJson2Geometry(string geoJson)
        {
            if (string.IsNullOrEmpty(geoJson))
                throw new ArgumentNullException(nameof(geoJson));

            return GeoJsonReader.Read<NetTopologySuite.Geometries.Geometry>(geoJson);
        }

        /// <summary>
        /// GeoJSON转WKT
        /// </summary>
        /// <param name="geoJson">GeoJSON格式的字符串</param>
        /// <returns>WKT格式的字符串</returns>
        public static string GeoJson2Wkt(string geoJson)
        {
            var geometry = GeoJson2Geometry(geoJson);
            return Geometry2Wkt(geometry);
        }

        // ==================== NTS Geometry转换方法 ====================

        /// <summary>
        /// NTS Geometry转WKT
        /// </summary>
        /// <param name="geometry">NTS Geometry对象</param>
        /// <returns>WKT格式的字符串</returns>
        public static string Geometry2Wkt(NetTopologySuite.Geometries.Geometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            return WktWriter.Write(geometry);
        }

        /// <summary>
        /// NTS Geometry转GeoJSON
        /// </summary>
        /// <param name="geometry">NTS Geometry对象</param>
        /// <returns>GeoJSON格式的字符串</returns>
        public static string Geometry2GeoJson(NetTopologySuite.Geometries.Geometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            return GeoJsonWriter.Write(geometry);
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 尝试解析WKT字符串
        /// </summary>
        /// <param name="wkt">WKT格式的字符串</param>
        /// <param name="geometry">输出的Geometry对象</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParseWkt(string wkt, out NetTopologySuite.Geometries.Geometry? geometry)
        {
            geometry = null;
            if (string.IsNullOrEmpty(wkt))
                return false;

            try
            {
                geometry = WktReader.Read(wkt);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试解析GeoJSON字符串
        /// </summary>
        /// <param name="geoJson">GeoJSON格式的字符串</param>
        /// <param name="geometry">输出的Geometry对象</param>
        /// <returns>是否解析成功</returns>
        public static bool TryParseGeoJson(string geoJson, out NetTopologySuite.Geometries.Geometry? geometry)
        {
            geometry = null;
            if (string.IsNullOrEmpty(geoJson))
                return false;

            try
            {
                geometry = GeoJsonReader.Read<NetTopologySuite.Geometries.Geometry>(geoJson);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
