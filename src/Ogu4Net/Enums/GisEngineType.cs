namespace Ogu4Net.Enums
{
    /// <summary>
    /// GIS引擎类型枚举
    /// </summary>
    public enum GisEngineType
    {
        /// <summary>
        /// NetTopologySuite引擎（.NET原生）
        /// </summary>
        Nts,

        /// <summary>
        /// GDAL引擎
        /// </summary>
        Gdal,

        /// <summary>
        /// 自动选择，NTS优先
        /// </summary>
        Auto
    }

    /// <summary>
    /// GisEngineType 扩展方法类
    /// </summary>
    public static class GisEngineTypeExtensions
    {
        /// <summary>
        /// 获取引擎描述
        /// </summary>
        public static string GetDescription(this GisEngineType type)
        {
            switch (type)
            {
                case GisEngineType.Nts: return "NetTopologySuite";
                case GisEngineType.Gdal: return "GDAL";
                case GisEngineType.Auto: return "自动选择，NTS优先";
                default: return type.ToString();
            }
        }

        /// <summary>
        /// 获取实际使用的引擎类型
        /// </summary>
        public static GisEngineType GetActualEngineType(this GisEngineType type)
        {
            if (type == GisEngineType.Auto)
            {
                // 默认使用NTS引擎，因为它是.NET原生支持
                return GisEngineType.Nts;
            }
            return type;
        }
    }
}
