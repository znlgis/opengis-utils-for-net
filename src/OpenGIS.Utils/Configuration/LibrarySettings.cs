namespace OpenGIS.Utils.Configuration
{
    /// <summary>
    /// 库设置
    /// </summary>
    public static class LibrarySettings
    {
        /// <summary>
        /// 默认坐标系容差
        /// </summary>
        public static double DefaultTolerance { get; set; } = 0.0001;

        /// <summary>
        /// 是否自动创建空间索引
        /// </summary>
        public static bool AutoCreateSpatialIndex { get; set; } = true;

        /// <summary>
        /// 空间索引阈值（要素数量）
        /// </summary>
        public static int SpatialIndexThreshold { get; set; } = 1000;

        /// <summary>
        /// 默认缓冲区精度
        /// </summary>
        public static int DefaultBufferSegments { get; set; } = 8;

        /// <summary>
        /// 是否使用 GDAL 异常
        /// </summary>
        public static bool UseGdalExceptions { get; set; } = true;
    }
}
