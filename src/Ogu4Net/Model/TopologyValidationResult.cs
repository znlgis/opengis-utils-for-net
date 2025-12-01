using NetTopologySuite.Geometries;
using Ogu4Net.Enums;

namespace Ogu4Net.Model
{
    /// <summary>
    /// 拓扑验证结果模型
    /// </summary>
    public class TopologyValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误位置坐标
        /// </summary>
        public Coordinate? Coordinate { get; set; }

        /// <summary>
        /// 错误类型
        /// </summary>
        public TopologyValidationErrorType? ErrorType { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TopologyValidationResult()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public TopologyValidationResult(bool isValid, Coordinate? coordinate = null, TopologyValidationErrorType? errorType = null, string? message = null)
        {
            IsValid = isValid;
            Coordinate = coordinate;
            ErrorType = errorType;
            Message = message;
        }
    }
}
