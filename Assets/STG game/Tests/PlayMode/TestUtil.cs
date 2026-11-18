using System.Reflection;
using NUnit.Framework;

// 測試用工具：設定元件上的 private [SerializeField] 欄位 (Inspector 才會填的引用)
internal static class TestUtil
{
    public static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = null;
        for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        Assert.IsNotNull(field, target.GetType().Name + " has no field " + fieldName);
        field.SetValue(target, value);
    }
}
