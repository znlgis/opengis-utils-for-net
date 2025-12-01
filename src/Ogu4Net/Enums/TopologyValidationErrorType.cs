namespace Ogu4Net.Enums
{
    /// <summary>
    /// 拓扑验证错误类型枚举
    /// </summary>
    public enum TopologyValidationErrorType
    {
        /// <summary>
        /// 一般拓扑检查错误
        /// </summary>
        Error = 0,

        /// <summary>
        /// 点重叠
        /// </summary>
        RepeatedPoint = 1,

        /// <summary>
        /// 洞在图形外
        /// </summary>
        HoleOutsideShell = 2,

        /// <summary>
        /// 洞重叠
        /// </summary>
        NestedHoles = 3,

        /// <summary>
        /// 图形内部不连通
        /// </summary>
        DisconnectedInterior = 4,

        /// <summary>
        /// 自相交
        /// </summary>
        SelfIntersection = 5,

        /// <summary>
        /// 环自相交
        /// </summary>
        RingSelfIntersection = 6,

        /// <summary>
        /// 图形重叠
        /// </summary>
        NestedShells = 7,

        /// <summary>
        /// 环重复
        /// </summary>
        DuplicateRings = 8,

        /// <summary>
        /// 点太少无法构成有效几何
        /// </summary>
        TooFewPoints = 9,

        /// <summary>
        /// 无效坐标
        /// </summary>
        InvalidCoordinate = 10,

        /// <summary>
        /// 环未闭合
        /// </summary>
        RingNotClosed = 11
    }

    /// <summary>
    /// TopologyValidationErrorType 扩展方法类
    /// </summary>
    public static class TopologyValidationErrorTypeExtensions
    {
        /// <summary>
        /// 获取错误描述
        /// </summary>
        public static string GetDescription(this TopologyValidationErrorType type)
        {
            switch (type)
            {
                case TopologyValidationErrorType.Error: return "拓扑检查错误";
                case TopologyValidationErrorType.RepeatedPoint: return "点重叠";
                case TopologyValidationErrorType.HoleOutsideShell: return "洞在图形外";
                case TopologyValidationErrorType.NestedHoles: return "洞重叠";
                case TopologyValidationErrorType.DisconnectedInterior: return "图形内部不连通";
                case TopologyValidationErrorType.SelfIntersection: return "自相交";
                case TopologyValidationErrorType.RingSelfIntersection: return "环自相交";
                case TopologyValidationErrorType.NestedShells: return "图形重叠";
                case TopologyValidationErrorType.DuplicateRings: return "环重复";
                case TopologyValidationErrorType.TooFewPoints: return "点太少无法构成有效几何";
                case TopologyValidationErrorType.InvalidCoordinate: return "无效坐标";
                case TopologyValidationErrorType.RingNotClosed: return "环未闭合";
                default: return "未知错误";
            }
        }

        /// <summary>
        /// 根据错误类型码获取枚举
        /// </summary>
        public static TopologyValidationErrorType? FromErrorType(int errorType)
        {
            if (errorType >= 0 && errorType <= 11)
            {
                return (TopologyValidationErrorType)errorType;
            }
            return null;
        }
    }
}
