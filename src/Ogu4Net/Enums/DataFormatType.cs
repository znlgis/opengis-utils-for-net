namespace Ogu4Net.Enums
{
    /// <summary>
    /// 支持的GIS数据格式枚举
    /// </summary>
    public enum DataFormatType
    {
        /// <summary>
        /// WKT格式
        /// </summary>
        Wkt,

        /// <summary>
        /// GeoJSON格式
        /// </summary>
        GeoJson,

        /// <summary>
        /// ESRI JSON格式
        /// </summary>
        EsriJson,

        /// <summary>
        /// Shapefile格式
        /// </summary>
        Shp,

        /// <summary>
        /// 国土TXT格式
        /// </summary>
        Txt,

        /// <summary>
        /// FileGDB格式
        /// </summary>
        FileGdb,

        /// <summary>
        /// PostGIS格式
        /// </summary>
        PostGis,

        /// <summary>
        /// ArcSDE格式
        /// </summary>
        ArcSde
    }

    /// <summary>
    /// DataFormatType 扩展方法类
    /// </summary>
    public static class DataFormatTypeExtensions
    {
        /// <summary>
        /// 获取格式描述
        /// </summary>
        public static string GetDescription(this DataFormatType type)
        {
            switch (type)
            {
                case DataFormatType.Wkt: return "WKT";
                case DataFormatType.GeoJson: return "GeoJSON";
                case DataFormatType.EsriJson: return "ESRI JSON";
                case DataFormatType.Shp: return "SHP文件";
                case DataFormatType.Txt: return "国土TXT";
                case DataFormatType.FileGdb: return "FileGDB";
                case DataFormatType.PostGis: return "PostGIS";
                case DataFormatType.ArcSde: return "ArcSDE";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 获取GDAL驱动名称（如果适用）
        /// </summary>
        public static string? GetGdalDriverName(this DataFormatType type)
        {
            switch (type)
            {
                case DataFormatType.GeoJson: return "GeoJSON";
                case DataFormatType.EsriJson: return "ESRIJSON";
                case DataFormatType.Shp: return "ESRI Shapefile";
                case DataFormatType.FileGdb: return "OpenFileGDB";
                case DataFormatType.PostGis: return "PostgreSQL";
                default: return null;
            }
        }
    }
}
