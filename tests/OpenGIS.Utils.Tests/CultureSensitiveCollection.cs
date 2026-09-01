using Xunit;

namespace OpenGIS.Utils.Tests;

/// <summary>
///     xunit 集合定义：将修改全局 CultureInfo.CurrentCulture 的测试类串行化，
///     避免并行执行时相互干扰。
/// </summary>
[CollectionDefinition("CultureSensitive", DisableParallelization = true)]
public class CultureSensitiveCollection
{
}
