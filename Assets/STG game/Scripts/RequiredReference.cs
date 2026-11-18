using UnityEngine;

// Inspector 必填欄位的檢查：留空時記錄指名欄位的錯誤並停用元件，取代之後才發生、難以追查的 NullReferenceException
public static class RequiredReference
{
    // 回傳 true 表示欄位缺漏 (已記錄錯誤並停用 owner)，呼叫端應立即 return
    public static bool IsMissing(Object value, string fieldName, Behaviour owner)
    {
        if (value != null) return false;

        Debug.LogError("[" + owner.GetType().Name + "] 必填欄位 " + fieldName + " 未指定", owner);
        owner.enabled = false;
        return true;
    }
}
