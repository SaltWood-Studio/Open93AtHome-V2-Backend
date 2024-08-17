using System;
using System.Collections.Generic;

namespace Open93AtHome.Modules;

public class MultiKeyDictionary<TKey1, TKey2, TValue> where TKey1 : notnull where TKey2 : notnull
{
    private readonly Dictionary<TKey1, TValue> _key1Dictionary;
    private readonly Dictionary<TKey2, TKey1> _key2Dictionary;

    public MultiKeyDictionary()
    {
        _key1Dictionary = new Dictionary<TKey1, TValue>();
        _key2Dictionary = new Dictionary<TKey2, TKey1>();
    }

    // 添加或更新键值对
    public void Add(TKey1 key1, TKey2 key2, TValue value)
    {
        if (_key1Dictionary.ContainsKey(key1))
        {
            // 如果 key1 已存在，更新值
            _key1Dictionary[key1] = value;
        }
        else
        {
            _key1Dictionary.Add(key1, value);
        }

        if (_key2Dictionary.ContainsKey(key2))
        {
            // 如果 key2 已存在，更新 key1
            _key2Dictionary[key2] = key1;
        }
        else
        {
            _key2Dictionary.Add(key2, key1);
        }
    }

    // 通过 key1 查找值
    public TValue? GetByKey1(TKey1 key1)
    {
        _key1Dictionary.TryGetValue(key1, out TValue? value);
        return value;
    }

    // 通过 key2 查找值
    public TValue? GetByKey2(TKey2 key2)
    {
        if (_key2Dictionary.TryGetValue(key2, out TKey1? key1))
        {
            return GetByKey1(key1);
        }
        return default;
    }

    // 移除键值对
    public void RemoveByKey1(TKey1 key1)
    {
        if (_key1Dictionary.TryGetValue(key1, out TValue? value))
        {
            _key1Dictionary.Remove(key1);
            // 移除与 key1 关联的 key2
            foreach (var pair in _key2Dictionary)
            {
                if (pair.Value.Equals(key1))
                {
                    _key2Dictionary.Remove(pair.Key);
                    break; // 只移除第一个匹配的 key2
                }
            }
        }
    }

    public void RemoveByKey2(TKey2 key2)
    {
        if (_key2Dictionary.TryGetValue(key2, out TKey1? key1))
        {
            _key2Dictionary.Remove(key2);
            _key1Dictionary.Remove(key1);
        }
    }
}