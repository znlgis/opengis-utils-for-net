using System;

namespace Ogu4Net.Enums
{
    /// <summary>
    /// 字段数据类型枚举
    /// </summary>
    public enum FieldDataType
    {
        /// <summary>
        /// 整型
        /// </summary>
        Integer = 0,

        /// <summary>
        /// 浮点型
        /// </summary>
        Double = 2,

        /// <summary>
        /// 字符串
        /// </summary>
        String = 4,

        /// <summary>
        /// 二进制
        /// </summary>
        Binary = 8,

        /// <summary>
        /// 日期
        /// </summary>
        Date = 9,

        /// <summary>
        /// 时间
        /// </summary>
        Time = 10,

        /// <summary>
        /// 日期时间
        /// </summary>
        DateTime = 11,

        /// <summary>
        /// 长整型
        /// </summary>
        Long = 12
    }

    /// <summary>
    /// FieldDataType 扩展方法类
    /// </summary>
    public static class FieldDataTypeExtensions
    {
        /// <summary>
        /// 获取对应的.NET类型
        /// </summary>
        public static Type GetClrType(this FieldDataType type)
        {
            switch (type)
            {
                case FieldDataType.Integer: return typeof(int);
                case FieldDataType.Double: return typeof(double);
                case FieldDataType.String: return typeof(string);
                case FieldDataType.Binary: return typeof(byte[]);
                case FieldDataType.Date: return typeof(DateTime);
                case FieldDataType.Time: return typeof(TimeSpan);
                case FieldDataType.DateTime: return typeof(DateTime);
                case FieldDataType.Long: return typeof(long);
                default: return typeof(string);
            }
        }

        /// <summary>
        /// 根据GDAL代码获取字段类型
        /// </summary>
        public static FieldDataType FromGdalCode(int gdalCode)
        {
            switch (gdalCode)
            {
                case 0: return FieldDataType.Integer;
                case 2: return FieldDataType.Double;
                case 1:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 13: return FieldDataType.String;
                case 8: return FieldDataType.Binary;
                case 9: return FieldDataType.Date;
                case 10: return FieldDataType.Time;
                case 11: return FieldDataType.DateTime;
                case 12: return FieldDataType.Long;
                default: return FieldDataType.String;
            }
        }

        /// <summary>
        /// 根据CLR类型获取字段类型
        /// </summary>
        public static FieldDataType FromClrType(Type clrType)
        {
            if (clrType == null)
                return FieldDataType.String;

            if (clrType == typeof(int) || clrType == typeof(int?))
                return FieldDataType.Integer;
            if (clrType == typeof(double) || clrType == typeof(double?) || clrType == typeof(float) || clrType == typeof(float?))
                return FieldDataType.Double;
            if (clrType == typeof(string))
                return FieldDataType.String;
            if (clrType == typeof(byte[]))
                return FieldDataType.Binary;
            if (clrType == typeof(DateTime) || clrType == typeof(DateTime?))
                return FieldDataType.DateTime;
            if (clrType == typeof(TimeSpan) || clrType == typeof(TimeSpan?))
                return FieldDataType.Time;
            if (clrType == typeof(long) || clrType == typeof(long?))
                return FieldDataType.Long;

            return FieldDataType.String;
        }
    }
}
