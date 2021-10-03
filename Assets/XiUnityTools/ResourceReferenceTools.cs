// =============================================================================
// MIT License
// 
// Copyright (c) 2018 Valeriya Pudova (hww.github.io)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// =============================================================================

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XiCore;
using XiUnityTools;

public static class ResourceReferenceTools
{
    private const string ASSETS_PREFIX = "/ast/";
 
    /// <summary>
    /// Get path to the MonoBehaviour, GameObject or Asset
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string GetReference(UnityEngine.Object obj)
    {
#if UNITY_EDITOR
        var go = obj as GameObject;
        if (go != null)
            return go.GetFullPath();
        var mb = obj as MonoBehaviour;
        if (mb != null)
            return mb.gameObject.GetFullPath();
        return ASSETS_PREFIX + AssetDatabase.GetAssetPath(obj);
#else
        throw new System.Exception();
#endif
    }
        
    /// <summary>
    /// Find all matched fields for this type of data
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static FieldInfo[] GetFieldInfos(UnityEngine.Object obj)
    {
        Type myType = obj.GetType();
        FieldInfo[] allFields = myType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        FieldInfo[] resFields = new FieldInfo[allFields.Length];
        var resFieldsCount = 0;
        for(int i = 0; i < allFields.Length; i++)
        {
            var field = allFields[i];
            if (field.IsNotSerialized)
                continue;
            var smartReference = field.GetCustomAttribute<ResourceReferenceAttr>();
            if (smartReference != null)
            {
                resFields[resFieldsCount++] = field;
            }
        }
        System.Array.Resize(ref resFields, resFieldsCount);
        return resFields;
    }

    /// <summary>
    /// Find all matched fields for this type of data
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="value">Find only with this value</param>
    /// <returns></returns>
    public static FieldInfo[] GetFieldInfos(UnityEngine.Object obj, string value)
    {
        Type myType = obj.GetType();
        FieldInfo[] allFields = myType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        FieldInfo[] resFields = new FieldInfo[allFields.Length];
        var resFieldsCount = 0;
        for(int i = 0; i < allFields.Length; i++)
        {
            var field = allFields[i];
            if (field.IsNotSerialized)
                continue;
            var smartReference = field.GetCustomAttribute<ResourceReferenceAttr>();
            if (smartReference != null)
            {
                if (field.GetValue(obj).ToString() == value)
                    resFields[resFieldsCount++] = field;
            }
        }
        System.Array.Resize(ref resFields, resFieldsCount);
        return resFields;
    }
}
