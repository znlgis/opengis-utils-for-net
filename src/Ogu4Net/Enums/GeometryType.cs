using System;
using NetTopologySuite.Geometries;

namespace Ogu4Net.Enums
{
    /// <summary>
    /// GIS图形类型枚举
    /// </summary>
    public enum GeometryType
    {
        /// <summary>
        /// 点
        /// </summary>
        Point = 0,

        /// <summary>
        /// 多点
        /// </summary>
        MultiPoint = 1,

        /// <summary>
        /// 线
        /// </summary>
        LineString = 2,

        /// <summary>
        /// 环
        /// </summary>
        LinearRing = 3,

        /// <summary>
        /// 多线
        /// </summary>
        MultiLineString = 4,

        /// <summary>
        /// 面
        /// </summary>
        Polygon = 5,

        /// <summary>
        /// 多面
        /// </summary>
        MultiPolygon = 6,

        /// <summary>
        /// 几何集合
        /// </summary>
        GeometryCollection = 7
    }

    /// <summary>
    /// GeometryType 扩展方法类
    /// </summary>
    public static class GeometryTypeExtensions
    {
        /// <summary>
        /// 获取类型名称
        /// </summary>
        public static string GetTypeName(this GeometryType type)
        {
            return type.ToString();
        }

        /// <summary>
        /// 获取对应的NTS类型
        /// </summary>
        public static Type GetNtsType(this GeometryType type)
        {
            switch (type)
            {
                case GeometryType.Point: return typeof(Point);
                case GeometryType.MultiPoint: return typeof(MultiPoint);
                case GeometryType.LineString: return typeof(LineString);
                case GeometryType.LinearRing: return typeof(LinearRing);
                case GeometryType.MultiLineString: return typeof(MultiLineString);
                case GeometryType.Polygon: return typeof(Polygon);
                case GeometryType.MultiPolygon: return typeof(MultiPolygon);
                case GeometryType.GeometryCollection: return typeof(GeometryCollection);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        /// <summary>
        /// 获取WKB类型码
        /// </summary>
        public static int GetWkbGeometryType(this GeometryType type)
        {
            switch (type)
            {
                case GeometryType.Point: return 1;
                case GeometryType.MultiPoint: return 4;
                case GeometryType.LineString: return 2;
                case GeometryType.LinearRing: return 101;
                case GeometryType.MultiLineString: return 5;
                case GeometryType.Polygon: return 3;
                case GeometryType.MultiPolygon: return 6;
                case GeometryType.GeometryCollection: return 7;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        /// <summary>
        /// 根据类型名称获取枚举
        /// </summary>
        public static GeometryType? FromTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            foreach (GeometryType type in Enum.GetValues(typeof(GeometryType)))
            {
                if (type.ToString().Equals(typeName, StringComparison.OrdinalIgnoreCase))
                    return type;
            }
            return null;
        }

        /// <summary>
        /// 根据NTS类型获取枚举
        /// </summary>
        public static GeometryType? FromNtsType(Type ntsType)
        {
            if (ntsType == null)
                return null;

            foreach (GeometryType type in Enum.GetValues(typeof(GeometryType)))
            {
                if (type.GetNtsType() == ntsType)
                    return type;
            }
            return null;
        }

        /// <summary>
        /// 根据WKB类型获取枚举
        /// </summary>
        public static GeometryType? FromWkbGeometryType(int wkbType)
        {
            foreach (GeometryType type in Enum.GetValues(typeof(GeometryType)))
            {
                if (type.GetWkbGeometryType() == wkbType)
                    return type;
            }
            return null;
        }
    }
}
