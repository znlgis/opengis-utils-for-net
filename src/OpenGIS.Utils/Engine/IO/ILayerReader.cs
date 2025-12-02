using System.Collections.Generic;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine.IO
{
    /// <summary>
    /// 图层读取接口
    /// </summary>
    public interface ILayerReader
    {
        /// <summary>
        /// 读取图层
        /// </summary>
        /// <param name="path">数据源路径</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="attributeFilter">属性过滤条件</param>
        /// <param name="spatialFilterWkt">空间过滤几何（WKT格式）</param>
        /// <param name="options">附加选项</param>
        /// <returns>图层对象</returns>
        OguLayer Read(string path, string? layerName = null, string? attributeFilter = null, string? spatialFilterWkt = null, Dictionary<string, object>? options = null);

        /// <summary>
        /// 获取图层名称列表
        /// </summary>
        /// <param name="path">数据源路径</param>
        /// <returns>图层名称列表</returns>
        IList<string> GetLayerNames(string path);
    }
}
