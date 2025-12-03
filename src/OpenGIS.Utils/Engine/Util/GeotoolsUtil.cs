using System;

namespace OpenGIS.Utils.Engine.Util
{
    /// <summary>
    /// GeoTools 工具类（对应 NetTopologySuite 的辅助功能）
    /// </summary>
    public static class GeotoolsUtil
    {
        /// <summary>
        /// 初始化 GeoTools 环境
        /// </summary>
        public static void Initialize()
        {
            // NetTopologySuite 不需要特殊初始化
            // 此方法保留以保持与 Java 版本的一致性
        }

        /// <summary>
        /// 获取版本信息
        /// </summary>
        public static string GetVersion()
        {
            var assembly = typeof(NetTopologySuite.Geometries.Geometry).Assembly;
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
    }
}
