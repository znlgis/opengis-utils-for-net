namespace OpenGIS.Utils.Engine.Model
{
    /// <summary>
    /// 数据库连接基础模型
    /// </summary>
    public class DbConnBaseModel
    {
        /// <summary>
        /// 主机
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// 端口
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string? Database { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Schema
        /// </summary>
        public string? Schema { get; set; }

        /// <summary>
        /// 连接字符串
        /// </summary>
        public string? ConnectionString { get; set; }
    }
}
