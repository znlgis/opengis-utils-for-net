namespace Ogu4Net.Model
{
    /// <summary>
    /// 数据库连接信息基类
    /// </summary>
    public class DbConnBaseModel
    {
        /// <summary>
        /// 数据库类型
        /// </summary>
        public string? DbType { get; set; }

        /// <summary>
        /// 数据库地址
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// 数据库端口
        /// </summary>
        public string? Port { get; set; }

        /// <summary>
        /// 数据库Schema
        /// </summary>
        public string? Schema { get; set; }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string? Database { get; set; }

        /// <summary>
        /// 数据库用户名
        /// </summary>
        public string? User { get; set; }

        /// <summary>
        /// 数据库密码
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public DbConnBaseModel()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DbConnBaseModel(string? dbType, string? host, string? port, string? schema, string? database, string? user, string? password)
        {
            DbType = dbType;
            Host = host;
            Port = port;
            Schema = schema;
            Database = database;
            User = user;
            Password = password;
        }
    }
}
