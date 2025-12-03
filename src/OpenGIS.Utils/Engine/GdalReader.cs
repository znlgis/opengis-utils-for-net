using System;
using System.Collections.Generic;
using OpenGIS.Utils.Engine.IO;
using OpenGIS.Utils.Engine.Model.Layer;

namespace OpenGIS.Utils.Engine
{
    /// <summary>
    /// GDAL 读取器
    /// </summary>
    public class GdalReader : ILayerReader
    {
        /// <summary>
        /// 读取图层
        /// </summary>
        public OguLayer Read(string path, string? layerName = null, string? attributeFilter = null, string? spatialFilterWkt = null, Dictionary<string, object>? options = null)
        {
            // TODO: Implement GDAL-based reading
            throw new NotImplementedException("GdalReader.Read is not yet implemented");
        }

        /// <summary>
        /// 获取图层名称列表
        /// </summary>
        public IList<string> GetLayerNames(string path)
        {
            // TODO: Implement layer name enumeration using GDAL
            throw new NotImplementedException("GdalReader.GetLayerNames is not yet implemented");
        }
    }
}
