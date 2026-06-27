using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AntiPebbleNG;

internal static class SpanExtension
{
    public static Span<byte> Read<T>(this Span<byte> bytes, out T value)
    {
        Span<byte> newPos = bytes.Read(typeof(T), out object o);
        value = (T)o;
        return newPos;
    }

    public static Span<byte> Read(this Span<byte> bytes, Type type, out object value)
    {
        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);
        else if (!type.IsPrimitive)
        {
            if (type == typeof(string))
            {
                string s = Encoding.Default.GetString(bytes);
                value = s;
                return bytes[s.Length..];
            }

            value = Activator.CreateInstance(type, true)!;

            foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.IsArray)
                {
                    if (fieldInfo.FieldType == typeof(byte[]))
                    {
                        fieldInfo.SetValue(value, bytes.ToArray());
                        bytes = bytes[^0..];
                    }
                    else
                    {
                        Type elemType = fieldInfo.FieldType.GetElementType()!;
                        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                        while (bytes.Length > 0)
                        {
                            bytes = bytes.Read(elemType, out object o);
                            list.Add(o);
                        }

                        Array array = (Array)Activator.CreateInstance(fieldInfo.FieldType, list.Count)!;
                        list.CopyTo(array, 0);
                        fieldInfo.SetValue(value, array);
                    }
                }
                else
                {
                    bytes = bytes.Read(fieldInfo.FieldType, out object o);
                    fieldInfo.SetValue(value, o);
                }
            }

            return bytes;
        }

        int size = Marshal.SizeOf(type);

        Span<byte> valueBytes = bytes[..size];
        valueBytes.Reverse();
        value = Type.GetTypeCode(type) switch
        {
            TypeCode.UInt16 => BitConverter.ToUInt16(valueBytes),
            TypeCode.UInt32 => BitConverter.ToUInt32(valueBytes),
            TypeCode.UInt64 => BitConverter.ToUInt64(valueBytes),
            TypeCode.Int16 => BitConverter.ToInt16(valueBytes),
            TypeCode.Int32 => BitConverter.ToInt32(valueBytes),
            TypeCode.Int64 => BitConverter.ToInt64(valueBytes),
            TypeCode.Byte => valueBytes[0],
            _ => throw new ArgumentException($"Cant handle {type.Name}.")
        };
        return bytes[size..];
    }

    public static Span<byte> Write(this object obj)
    {
        Type type = obj.GetType();

        if (type.IsEnum)
            type = Enum.GetUnderlyingType(type);

        else if (!type.IsPrimitive)
        {
            if (type == typeof(string))
                return Encoding.Default.GetBytes((string)obj);

            List<byte[]> data = [];

            foreach (FieldInfo fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.IsArray)
                {
                    if (fieldInfo.FieldType == typeof(byte[]))
                        data.Add((byte[])fieldInfo.GetValue(obj)!);
                    else
                        data.Add([.. ((IEnumerable)fieldInfo.GetValue(obj)).Cast<object>().SelectMany(o => o!.Write().ToArray())]);
                }
                else
                    data.Add([.. fieldInfo.GetValue(obj)!.Write()]);
            }

            return data.SelectMany(x => x).ToArray();
        }

        Span<byte> valueBytes = (Type.GetTypeCode(type) switch
        {
            TypeCode.UInt16 => BitConverter.GetBytes((ushort)obj),
            TypeCode.UInt32 => BitConverter.GetBytes((uint)obj),
            TypeCode.UInt64 => BitConverter.GetBytes((ulong)obj),
            TypeCode.Int16 => BitConverter.GetBytes((short)obj),
            TypeCode.Int32 => BitConverter.GetBytes((int)obj),
            TypeCode.Int64 => BitConverter.GetBytes((long)obj),
            TypeCode.Byte => [(byte)obj],
            _ => throw new ArgumentException($"Cant handle {type.Name}.")
        }).AsSpan();
        valueBytes.Reverse();
        return valueBytes;
    }
}