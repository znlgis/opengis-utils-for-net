namespace Ogu4Net.Model.Layer
{
    /// <summary>
    /// OGU要素过滤器接口
    /// <para>
    /// 用于筛选图层中的要素。
    /// 可与OguLayer.Filter()方法配合使用。
    /// </para>
    /// </summary>
    /// <param name="feature">要判断的要素</param>
    /// <returns>true表示满足条件，false表示不满足</returns>
    public delegate bool OguFeatureFilter(OguFeature feature);
}
