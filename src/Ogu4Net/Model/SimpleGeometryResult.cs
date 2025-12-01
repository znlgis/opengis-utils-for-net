using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace Ogu4Net.Model
{
    /// <summary>
    /// 简单几何判断结果模型
    /// </summary>
    public class SimpleGeometryResult
    {
        /// <summary>
        /// 是否是简单几何
        /// </summary>
        public bool IsSimple { get; set; }

        /// <summary>
        /// 非简单几何点集合
        /// </summary>
        public List<Coordinate>? NonSimplePoints { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SimpleGeometryResult()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SimpleGeometryResult(bool isSimple, List<Coordinate>? nonSimplePoints = null)
        {
            IsSimple = isSimple;
            NonSimplePoints = nonSimplePoints;
        }
    }
}
