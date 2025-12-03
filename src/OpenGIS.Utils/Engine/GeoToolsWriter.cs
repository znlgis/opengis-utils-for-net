using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine
{
    /// <summary>
    /// GeoTools 写入器（基于 NetTopologySuite）
    /// </summary>
    public class GeoToolsWriter : ILayerWriter
    {
        /// <summary>
        /// 写入图层
        /// </summary>
        public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement GeoTools-based writing
            throw new NotImplementedException("GeoToolsWriter.Write is not yet implemented");
        }

        /// <summary>
        /// 追加要素到已存在的图层
        /// </summary>
        public void Append(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement append functionality
            throw new NotImplementedException("GeoToolsWriter.Append is not yet implemented");
        }
    }
}
