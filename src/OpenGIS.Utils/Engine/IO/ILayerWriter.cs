using System.Collections.Generic;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine.IO
{
    /// <summary>
    /// 图层写入接口
    /// </summary>
    public interface ILayerWriter
    {
        /// <summary>
        /// 写入图层
        /// </summary>
        /// <param name="layer">图层对象</param>
        /// <param name="path">输出路径</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="options">附加选项</param>
        void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null);

        /// <summary>
        /// 追加要素到已存在的图层
        /// </summary>
        /// <param name="layer">图层对象</param>
        /// <param name="path">输出路径</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="options">附加选项</param>
        void Append(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null);
    }
}
