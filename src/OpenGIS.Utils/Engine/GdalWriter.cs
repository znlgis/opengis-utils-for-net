using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine
{
    /// <summary>
    /// GDAL 写入器
    /// </summary>
    public class GdalWriter : ILayerWriter
    {
        /// <summary>
        /// 写入图层
        /// </summary>
        public void Write(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement GDAL-based writing
            throw new NotImplementedException("GdalWriter.Write is not yet implemented");
        }

        /// <summary>
        /// 追加要素到已存在的图层
        /// </summary>
        public void Append(OguLayer layer, string path, string? layerName = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement append functionality using GDAL
            throw new NotImplementedException("GdalWriter.Append is not yet implemented");
        }
    }
}
