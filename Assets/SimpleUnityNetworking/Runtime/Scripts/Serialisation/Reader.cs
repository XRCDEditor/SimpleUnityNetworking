using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;

namespace jKnepel.SimpleUnityNetworking.Serialisation
{
    public class Reader
    {
		#region fields

		/// <summary>
		/// The current byte position of the writer header within the buffer.
		/// </summary>
		/// <remarks>
		/// Do not use this position unless you know what you are doing! 
		/// Setting the position manually will not check for buffer bounds.
		/// </remarks>
		public int Position;
		/// <summary>
		/// The length of the given buffer in bytes.
		/// </summary>
		public int Length => _buffer.Length;
		/// <summary>
		/// The remaining positions until the full length of the buffer.
		/// </summary>
		public int Remaining => Length - Position;
		/// <summary>
		/// The configuration of the reader.
		/// </summary>
		public SerialiserConfiguration SerialiserConfiguration { get; }

		private readonly byte[] _buffer;

        private static readonly ConcurrentDictionary<Type, Func<Reader, object>> _typeHandlerCache = new();
        private static readonly HashSet<Type> _unknownTypes = new();

		#endregion

		#region lifecycle

		public Reader(byte[] bytes, SerialiserConfiguration config = null)
		{
			if (bytes == null)
				return;

			Position = 0;
			_buffer = bytes;
			SerialiserConfiguration = config ?? new();
		}

		#endregion

		#region automatic type handler

		public T Read<T>()
		{
            var type = typeof(T);
            return (T)Read(type);
        }

        private object Read(Type type)
		{
            if (!_unknownTypes.Contains(type))
            {
	            if (ReadBuildInType(type, out var result))
		            return result;

				// use custom type handler if user defined method was found
                if (CreateTypeHandlerDelegate(type, out var customHandler, true))
                    return customHandler(this);

                // TODO : remove this once pre-compile cached generic handlers are supported
                // use implemented generic type handler
                if (CreateTypeHandlerDelegate(type, out var implementedHandler, false))
                    return implementedHandler(this);

                // save types that don't have any a type handler and need to be recursively serialised
                _unknownTypes.Add(type);
            }

            // recursively serialise type if no handler is found
            // TODO : circular dependencies will cause crash
            // TODO : add attributes for serialisation
            // TODO : add serialisation options to handle size, circular dependencies etc. 
            // TODO : handle properties
            var fieldInfos = type.GetFields();
            if (fieldInfos.Length == 0 || fieldInfos.Any(x => x.FieldType == type))
            {   // TODO : circular dependencies will cause crash
                var typeName = SerialiserHelper.GetTypeName(type);
                throw new SerialiseNotImplemented($"No read method implemented for the type {typeName}!"
                    + $" Implement a Read{typeName} method or use an extension method in the parent type!");
			}

            var obj = FormatterServices.GetUninitializedObject(type);
            foreach (var fieldInfo in fieldInfos)
                fieldInfo.SetValue(obj, Read(fieldInfo.FieldType));
            return obj;
        }
        
        private bool ReadBuildInType(Type val, out object result)
        {
	        switch (val)
	        {
		        case not null when val == typeof(bool):
			        result = ReadBoolean();
			        return true;
		        case not null when val == typeof(byte):
			        result = ReadByte();
			        return true;
		        case not null when val == typeof(sbyte):
			        result = ReadSByte();
			        return true;
		        case not null when val == typeof(ushort):
			        result = ReadUInt16();
			        return true;
		        case not null when val == typeof(short):
			        result = ReadInt16();
			        return true;
		        case not null when val == typeof(uint):
			        result = ReadUInt32();
			        return true;
		        case not null when val == typeof(int):
			        result = ReadInt32();
			        return true;
		        case not null when val == typeof(ulong):
			        result = ReadUInt64();
			        return true;
		        case not null when val == typeof(long):
			        result = ReadInt64();
			        return true;
		        case not null when val == typeof(string):
			        result = ReadString();
			        return true;
		        case not null when val == typeof(char):
			        result = ReadChar();
			        return true;
		        case not null when val == typeof(float):
			        result = ReadSingle();
			        return true;
		        case not null when val == typeof(double):
			        result = ReadDouble();
			        return true;
		        case not null when val == typeof(decimal):
			        result = ReadDecimal();
			        return true;
		        case not null when val == typeof(Vector2):
			        result = ReadVector2();
			        return true;
		        case not null when val == typeof(Vector3):
			        result = ReadVector3();
			        return true;
		        case not null when val == typeof(Vector4):
			        result = ReadVector4();
			        return true;
		        case not null when val == typeof(Matrix4x4):
			        result = ReadMatrix4x4();
			        return true;
		        case not null when val == typeof(Color):
			        result = ReadColor();
			        return true;
		        case not null when val == typeof(Color32):
			        result = ReadColor32();
			        return true;
		        case not null when val == typeof(DateTime):
			        result = ReadDateTime();
			        return true;
		        default:
			        result = null;
			        return false;
	        }
        }

        /// <summary>
        /// Constructs and caches pre-compiled expression delegate of type handlers.
        /// </summary>
        /// <param name="type">The type of the variable for which the writer is defined</param>
        /// <param name="typeHandler">The handler of the defined type</param>
        /// <param name="useCustomReader">Whether the reader method is an instance of the Reader class or a custom static method in the type</param>
        /// <returns></returns>
        private static bool CreateTypeHandlerDelegate(Type type, out Func<Reader, object> typeHandler, bool useCustomReader)
        {   // find implemented or custom read method
            var readerMethod = useCustomReader
                ?           type.GetMethod("Read", BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                : typeof(Reader).GetMethod($"Read{SerialiserHelper.GetTypeName(type)}", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (readerMethod == null)
            {
	            typeHandler = null;
                return false;
            }

            // parameters
            var instanceArg = Expression.Parameter(typeof(Reader), "instance");

            // construct handler call body
            MethodCallExpression call;
            if (readerMethod.IsGenericMethod)
            {
                var genericReader = type.IsArray
                    ? readerMethod.MakeGenericMethod(type.GetElementType())
                    : readerMethod.MakeGenericMethod(type.GetGenericArguments());
                call = useCustomReader
                    ? Expression.Call(genericReader, instanceArg)
                    : Expression.Call(instanceArg, genericReader);
            }
            else
            {
                call = useCustomReader
                    ? Expression.Call(readerMethod, instanceArg)
                    : Expression.Call(instanceArg, readerMethod);
            }

            // cache delegate
            var castResult = Expression.Convert(call, typeof(object));
            var lambda = Expression.Lambda<Func<Reader, object>>(castResult, instanceArg);
            typeHandler = lambda.Compile();
            _typeHandlerCache.TryAdd(type, typeHandler);
            return true;
        }

		#endregion

		#region helpers

		/// <summary>
		/// Skips the reader header ahead by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public void Skip(int bytes)
		{
            if (bytes < 1 || bytes > Remaining)
                return;

            Position += bytes;
		}

		/// <summary>
		/// Reverts the reader header back by the given number of bytes.
		/// </summary>
		/// <param name="bytes"></param>
		public void Revert(int bytes)
        {
            Position -= bytes; 
            Position = Mathf.Max(Position, 0);
        }

		/// <summary>
		/// Clears the reader buffer.
		/// </summary>
		public void Clear()
		{
            Position += Remaining;
		}

		/// <returns>The full internal buffer.</returns>
		public byte[] GetFullBuffer()
		{
            return _buffer;
		}

		/// <summary>
		/// Reads a specified number of bytes from the internal buffer to a destination array starting at a particular offset.
		/// </summary>
		/// <param name="dst"></param>
		/// <param name="dstOffset"></param>
		/// <param name="count"></param>
		public void BlockCopy(ref byte[] dst, int dstOffset, int count)
		{
            Buffer.BlockCopy(_buffer, Position, dst, dstOffset, count);
            Position += count;
		}

		/// <returns>Reads and returns a byte segment of the specified length.</returns>
		public ArraySegment<byte> ReadByteSegment(int count)
		{
            if (count > Remaining)
                throw new IndexOutOfRangeException("The count exceeds the remaining length!");

            ArraySegment<byte> result = new(_buffer, Position, count);
            Position += count;
            return result;
        }

		/// <returns>Reads and returns a byte array of the specified length.</returns>
        public byte[] ReadByteArray(int count)
        {
            return ReadByteSegment(count).ToArray();
        }

		/// <returns>The remaining bytes.</returns>
		public byte[] ReadRemainingBuffer()
		{
            var remaining = new byte[Remaining];
            BlockCopy(ref remaining, 0, Remaining);
            return remaining;
		}

		#endregion

		#region primitives

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
		{
            var result = _buffer[Position++];
            return result == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
		{
            var result = _buffer[Position++];
            return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
		{
            var result = (sbyte)_buffer[Position++];
            return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return (ushort)ReadVLQCompression();
			
			ushort result = _buffer[Position++];
			result |= (ushort)(_buffer[Position++] << 8);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return (short)ZigZagDecode(ReadVLQCompression());
			
			short result = _buffer[Position++];
			result |= (short)(_buffer[Position++] << 8);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return (uint)ReadVLQCompression();
			
			uint result = _buffer[Position++];
			result |= (uint)(_buffer[Position++] << 8);
			result |= (uint)(_buffer[Position++] << 16);
			result |= (uint)(_buffer[Position++] << 24);
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return (int)ZigZagDecode(ReadVLQCompression());
			
			int result = _buffer[Position++];
			result |= _buffer[Position++] << 8;
			result |= _buffer[Position++] << 16;
			result |= _buffer[Position++] << 24;
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return ReadVLQCompression();
			
			ulong result = _buffer[Position++];
			result |= (ulong)_buffer[Position++] << 8;
			result |= (ulong)_buffer[Position++] << 16;
			result |= (ulong)_buffer[Position++] << 24;
			result |= (ulong)_buffer[Position++] << 32;
			result |= (ulong)_buffer[Position++] << 40;
			result |= (ulong)_buffer[Position++] << 48;
			result |= (ulong)_buffer[Position++] << 56;
			return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
				return ZigZagDecode(ReadVLQCompression());
			
			long result = _buffer[Position++];
			result |= (long)_buffer[Position++] << 8;
			result |= (long)_buffer[Position++] << 16;
			result |= (long)_buffer[Position++] << 24;
			result |= (long)_buffer[Position++] << 32;
			result |= (long)_buffer[Position++] << 40;
			result |= (long)_buffer[Position++] << 48;
			result |= (long)_buffer[Position++] << 56;
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar()
		{
            var result = (char)_buffer[Position++];
            result |= (char)(_buffer[Position++] << 8);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
			{
				var compressed = ZigZagDecode(ReadVLQCompression());
				return compressed / Mathf.Pow(10, SerialiserConfiguration.NumberOfDecimalPlaces);
			}
			
			TypeConverter.UIntToFloat converter = new() { UInt = ReadUInt32() };
			return converter.Float;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
		{
            TypeConverter.ULongToDouble converter = new() { ULong = ReadUInt64() };
            return converter.Double;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
		{
            TypeConverter.ULongsToDecimal converter = new() { ULong1 = ReadUInt64(), ULong2 = ReadUInt64() };
            return converter.Decimal;
        }

		#endregion

		#region unity objects

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ReadVector2()
		{
            return new(ReadSingle(), ReadSingle());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ReadVector3()
		{
            return new(ReadSingle(), ReadSingle(), ReadSingle());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ReadVector4()
		{
            return new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ReadQuaternion()
		{
			if (SerialiserConfiguration.UseCompression == EUseCompression.Compressed)
			{
				var packed = ReadVLQCompression();
				CompressedQuaternion q = new(packed, SerialiserConfiguration.BitsPerComponent);
				return q.Quaternion;
			}
			
            return new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ReadMatrix4x4()
		{
            return new()
			{
                m00 = ReadSingle(), m01 = ReadSingle(), m02 = ReadSingle(), m03 = ReadSingle(),
                m10 = ReadSingle(), m11 = ReadSingle(), m12 = ReadSingle(), m13 = ReadSingle(),
                m20 = ReadSingle(), m21 = ReadSingle(), m22 = ReadSingle(), m23 = ReadSingle(),
                m30 = ReadSingle(), m31 = ReadSingle(), m32 = ReadSingle(), m33 = ReadSingle()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadColor()
		{
            var r = ReadByte() / 100f;
            var g = ReadByte() / 100f;
            var b = ReadByte() / 100f;
            var a = ReadByte() / 100f;
            return new(r, g, b, a);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color ReadColorWithoutAlpha()
		{
            var r = ReadByte() / 100f;
            var g = ReadByte() / 100f;
            var b = ReadByte() / 100f;
            return new(r, g, b, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 ReadColor32()
		{
            return new(ReadByte(), ReadByte(), ReadByte(), ReadByte());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Color32 ReadColor32WithoutAlpha()
		{
            return new(ReadByte(), ReadByte(), ReadByte(), 255);
        }

		#endregion

		#region objects

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadString()
		{
            return Encoding.UTF8.GetString(ReadByteArray(ReadUInt16()));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ReadStringWithoutFlag(int length)
        {
            return Encoding.ASCII.GetString(ReadByteArray(length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ReadArray<T>()
		{
            var length = ReadInt32();
            var array = new T[length];
            for (var i = 0; i < length; i++)
                array[i] = Read<T>();
            return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> ReadList<T>()
		{
            var count = ReadInt32();
            List<T> list = new(count);
            for (var i = 0; i < count; i++)
                list.Add(Read<T>());
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
		{
            var count = ReadInt32();
            Dictionary<TKey, TValue> dictionary = new(count);
            for (var i = 0; i < count; i++)
                dictionary.Add(Read<TKey>(), Read<TValue>());
            return dictionary;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTime ReadDateTime()
		{
            return DateTime.FromBinary(ReadInt64());
		}

        #endregion
        
        #region utilities
        
		/// <summary>
		/// "ZigZagEncoding" based on google protocol buffers.
		/// See for <a href="https://protobuf.dev/programming-guides/encoding/">reference</a>.
		/// </summary>
		/// <param name="val"></param>
		/// <returns></returns>
        private static long ZigZagDecode(ulong val)
        {
	        return ((long)val >> 1) ^ -((long)val & 1);
        }

        /// <summary>
        /// Uses a 7-bit VLQ encoding scheme based on the MIDI compression system.
        /// See for <a href="https://web.archive.org/web/20051129113105/http://www.borg.com/~jglatt/tech/midifile/vari.htm">reference</a>.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadVLQCompression()
        {
	        ulong val = 0;
	        ulong shift = 0;
	        var left = true;
	        while (left)
	        {
		        ulong lowerBits = ReadByte();
		        left = (lowerBits & 0x80) != 0;
		        val |= (lowerBits & 0x7F) << (int)shift;
		        shift += 7;
	        }
	        
	        return val;
        }
        
        #endregion
    }
}
