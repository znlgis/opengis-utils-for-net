using System.Collections.Generic;

namespace Ogu4Net.Model
{
    /// <summary>
    /// GDB图层组模型
    /// </summary>
    public class GdbGroupModel
    {
        /// <summary>
        /// 图层组名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 图层名称列表
        /// </summary>
        public List<string>? LayerNames { get; set; }

        /// <summary>
        /// 子图层组列表
        /// </summary>
        public List<GdbGroupModel>? Groups { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public GdbGroupModel()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public GdbGroupModel(string? name, List<string>? layerNames = null, List<GdbGroupModel>? groups = null)
        {
            Name = name;
            LayerNames = layerNames;
            Groups = groups;
        }
    }
}
