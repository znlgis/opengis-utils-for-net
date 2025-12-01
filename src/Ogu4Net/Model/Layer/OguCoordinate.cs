namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU坐标类
    /// <para>
    /// 表示一个地理坐标点，支持二维/三维坐标。
    /// 包含点号和圈号属性，用于国土TXT格式等特殊场景。
    /// </para>
    /// </summary>
    public class OguCoordinate
    {
        /// <summary>
        /// X坐标（经度）
        /// </summary>
        public double? X { get; set; }

        /// <summary>
        /// Y坐标（纬度）
        /// </summary>
        public double? Y { get; set; }

        /// <summary>
        /// Z坐标（高程，可选）
        /// </summary>
        public double? Z { get; set; }

        /// <summary>
        /// 点号（用于TXT格式等特殊场景）
        /// </summary>
        public string? PointNumber { get; set; }

        /// <summary>
        /// 圈号/环号（用于多边形等场景）
        /// </summary>
        public int? RingNumber { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public OguCoordinate()
        {
        }

        /// <summary>
        /// 简化构造函数（二维坐标）
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        public OguCoordinate(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 简化构造函数（三维坐标）
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        public OguCoordinate(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 完整构造函数
        /// </summary>
        public OguCoordinate(double? x, double? y, double? z, string? pointNumber, int? ringNumber)
        {
            X = x;
            Y = y;
            Z = z;
            PointNumber = pointNumber;
            RingNumber = ringNumber;
        }
    }
}
