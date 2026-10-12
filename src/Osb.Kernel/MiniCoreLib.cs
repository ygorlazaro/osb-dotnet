// Minimal class library ("mini-CoreLib") for a freestanding NativeAOT kernel.
// Modeled on dotnet/runtime's Test.CoreLib + Runtime.Base, trimmed to the set
// of types/signatures the NativeAOT compiler (ilc) requires from the system module.
// No GC, no exception handling, no reflection: just enough to compile C# that
// runs bare metal.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Runtime;

namespace System
{
    public unsafe class Object
    {
#pragma warning disable 649
        private MethodTable* m_pEEType;
#pragma warning restore 649
        public Object() { }
        ~Object() { }
        public virtual bool Equals(object o) => false;
        public virtual int GetHashCode() => 0;
        public virtual string ToString() => null;
        internal MethodTable* GetMethodTable() => m_pEEType;
    }

    public struct Void { }

    // Primitives: the compiler special-cases these and their sizes; the private
    // self-typed field is the documented data contract with the runtime.
#pragma warning disable 0169
    public struct Boolean { private bool _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Char { private char _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SByte { private sbyte _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Byte { private byte _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int16 { private short _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt16 { private ushort _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int32 { private int _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt32 { private uint _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int64 { private long _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt64 { private ulong _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct IntPtr { private nint _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct UIntPtr { private nuint _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Single { private float _value; }
    [StructLayout(LayoutKind.Sequential)]
    public struct Double { private double _value; }
#pragma warning restore 0169

    public abstract class ValueType { }
    public abstract class Enum : ValueType { }

    public struct Nullable<T> where T : struct { }

    [StructLayout(LayoutKind.Sequential)]
    public class String
    {
#if TARGET_64BIT
        internal const int FIRST_CHAR_OFFSET = 8 + sizeof(int);
#else
        internal const int FIRST_CHAR_OFFSET = 4 + sizeof(int);
#endif
#pragma warning disable 169, 649
        private int _stringLength;
        private char _firstChar;
#pragma warning restore
        public int Length => _stringLength;
    }

    public class Array
    {
        public static void Ctor() => throw null;
        public static void CtorRare() => throw null;
#pragma warning disable 649
        // first field: the runtime/compiler depend on this data contract
        private int _numComponents;
#pragma warning restore 649
        public int Length => _numComponents;
    }

    // Generic arrays (T[]) are represented by this pseudo-type; the compiler
    // requires it to exist in the class library.
    internal class Array<T> : Array { }
    public abstract class Delegate { }
    public abstract class MulticastDelegate : Delegate { }

    public struct TypedReference
    {
        public static TypedReference GetRefAny(RuntimeTypeHandle type, ref byte value) => throw null;
    }

    public struct RuntimeTypeHandle
    {
#pragma warning disable 649
        private IntPtr _value;
#pragma warning restore 649
        internal RuntimeTypeHandle(IntPtr value) { _value = value; }
        internal static unsafe RuntimeTypeHandle GetRuntimeTypeHandleFromMethodTable(MethodTable* pMT)
            => default;
    }
    public struct RuntimeMethodHandle { }
    public struct RuntimeFieldHandle { }

    public class Attribute { }

    public abstract class Type
    {
        internal static unsafe Type GetTypeFromMethodTable(MethodTable* pMT) => null;
        internal static unsafe Type GetTypeFromMethodTableMaybeNull(MethodTable* pMT) => null;
    }

    public sealed class RuntimeType : Type { }

    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class FlagsAttribute : Attribute { }

    [Flags]
    public enum AttributeTargets
    {
        Assembly = 0x0001, Module = 0x0002, Class = 0x0004, Struct = 0x0008,
        Enum = 0x0010, Constructor = 0x0020, Method = 0x0040, Property = 0x0080,
        Field = 0x0100, Event = 0x0200, Interface = 0x0400, Parameter = 0x0800,
        Delegate = 0x1000, ReturnValue = 0x2000, GenericParameter = 0x4000,
        All = 0x7FFF,
    }

    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets validOn) { }
        public bool AllowMultiple { get; set; }
        public bool Inherited { get; set; }
    }

    public class Exception { }
    public sealed class SystemException : Exception { }

    public class AppContext
    {
        public static void SetData(string s, object o) { }
    }
}

namespace System.Runtime.CompilerServices
{
    public class RuntimeHelpers
    {
        public static unsafe int OffsetToStringData => sizeof(IntPtr) + sizeof(int);
    }

    public static class RuntimeFeature
    {
        public const string UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
        public const string ByRefLikeGenerics = nameof(ByRefLikeGenerics);
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IntrinsicAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class IsByRefLikeAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class IsVolatile : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FixedBufferAttribute : Attribute
    {
        public FixedBufferAttribute(Type elementType, int length) { }
    }

    public enum MethodImplOptions
    {
        Unmanaged = 0x0004,
        NoInlining = 0x0008,
        AggressiveInlining = 0x0100,
        InternalCall = 0x1000,
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MethodImplAttribute : Attribute
    {
        public MethodImplAttribute(MethodImplOptions methodImplOptions) { }
        public MethodImplAttribute(short value) { }
    }
}

namespace System.Runtime.InteropServices
{
    public enum LayoutKind { Sequential = 0, Explicit = 2, Auto = 3 }
    public enum CharSet { None = 1, Ansi = 2, Unicode = 3, Auto = 4 }
    public enum CallingConvention { Winapi = 1, Cdecl = 2, StdCall = 3 }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class StructLayoutAttribute : Attribute
    {
        public StructLayoutAttribute(LayoutKind layoutKind) { }
        public StructLayoutAttribute(short layoutKind) { }
        public CharSet CharSet;
        public int Pack;
        public int Size;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class FieldOffsetAttribute : Attribute
    {
        public FieldOffsetAttribute(int offset) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DllImportAttribute : Attribute
    {
        public DllImportAttribute(string dllName) { }
        public string EntryPoint;
        public CharSet CharSet;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnmanagedCallersOnlyAttribute : Attribute
    {
        public Type[] CallConvs;
        public string EntryPoint;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UnmanagedCallConvAttribute : Attribute { }

    public interface IDynamicInterfaceCastable
    {
        RuntimeTypeHandle GetDynamicInterfaceImplementation(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented);
        bool IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented);
    }
}

namespace System.Runtime
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class RuntimeExportAttribute : Attribute
    {
        public RuntimeExportAttribute(string entry) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class RuntimeImportAttribute : Attribute
    {
        public RuntimeImportAttribute(string dllName, string entry) { }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class AsyncHelpers
    {
        public static void AllocContinuation() => throw null;
    }

    public static unsafe class Unsafe
    {
        [Intrinsic] public static IntPtr ByteOffset<T>(ref readonly T origin, ref readonly T target) => throw null;
        [Intrinsic] public static void* AsPointer<T>(ref readonly T value) => throw null;
        [Intrinsic] public static int SizeOf<T>() => throw null;
        [Intrinsic] public static T As<T>(object value) where T : class => throw null;
        [Intrinsic] public static ref TTo As<TFrom, TTo>(ref TFrom source) => throw null;
        [Intrinsic] public static ref T AddByteOffset<T>(ref T source, IntPtr byteOffset) => throw null;
        [Intrinsic] public static ref T AsRef<T>(in T source) => throw null;
        [Intrinsic] public static T ReadUnaligned<T>(void* source) => throw null;
        [Intrinsic] public static T ReadUnaligned<T>(ref readonly byte source) => throw null;
        [Intrinsic] public static void CopyBlock(void* destination, void* source, uint byteCount) => throw null;
        [Intrinsic] public static void CopyBlock(ref byte destination, ref readonly byte source, uint byteCount) => throw null;

        public static ref T Add<T>(ref T source, int elementOffset) =>
            ref AddByteOffset(ref source, (IntPtr)(elementOffset * (nint)SizeOf<T>()));
        public static ref T Add<T>(ref T source, IntPtr elementOffset) =>
            ref AddByteOffset(ref source, (IntPtr)((nint)elementOffset * (nint)SizeOf<T>()));
    }
}

namespace System
{
    internal static partial class SpanHelpers
    {
        [Intrinsic]
        public static unsafe void ClearWithoutReferences(ref byte dest, nuint len) => Fill(ref dest, 0, len);

        [Intrinsic]
        internal static unsafe void Memmove(ref byte dest, ref byte src, nuint len)
        {
            if ((nuint)(nint)Unsafe.ByteOffset(ref src, ref dest) >= len)
                for (nuint i = 0; i < len; i++)
                    Unsafe.Add(ref dest, (nint)i) = Unsafe.Add(ref src, (nint)i);
            else
                for (nuint i = len; i > 0; i--)
                    Unsafe.Add(ref dest, (nint)(i - 1)) = Unsafe.Add(ref src, (nint)(i - 1));
        }

        internal static void Fill(ref byte dest, byte value, nuint len)
        {
            for (nuint i = 0; i < len; i++)
                Unsafe.Add(ref dest, (nint)i) = value;
        }
    }

    public static unsafe partial class Buffer
    {
        public static unsafe void Memmove(byte* dest, byte* src, nuint len)
        {
            if (dest == src || len == 0) return;
            if (dest < src) { for (nuint i = 0; i < len; i++) dest[i] = src[i]; }
            else { for (nuint i = len; i > 0; i--) dest[i - 1] = src[i - 1]; }
        }
        public static void Memcpy(byte* dest, byte* src, nuint len) => Memmove(dest, src, len);
        internal static void BulkMoveWithWriteBarrier(ref byte destination, ref byte source, nuint byteCount)
            => SpanHelpers.Memmove(ref destination, ref source, byteCount);
    }
}

namespace Internal.Runtime
{
    // Opaque, compiler-known type. We only ever use MethodTable*.
    internal unsafe struct MethodTable { }

    internal enum ExceptionStringID { }
}

namespace System.Runtime
{
    // Cast/array helpers referenced by the NativeAOT compiler. Never executed in
    // this kernel (we do no casts, allocations or array stores), so fail-fast.
    internal static unsafe class TypeCast
    {
        public static void StelemRef(object[] array, nint index, object obj) => throw null;
        public static ref object LdelemaRef(object[] array, nint index, MethodTable* elementType) => throw null;
        public static object CheckCastAny(MethodTable* pTargetType, object obj) => throw null;
        public static object CheckCastInterface(MethodTable* pTargetType, object obj) => throw null;
        public static object CheckCastClass(MethodTable* pTargetType, object obj) => throw null;
        public static object CheckCastClassSpecial(MethodTable* pTargetType, object obj) => throw null;
        public static object IsInstanceOfAny(MethodTable* pTargetType, object obj) => throw null;
        public static object IsInstanceOfInterface(MethodTable* pTargetType, object obj) => throw null;
        public static object IsInstanceOfClass(MethodTable* pTargetType, object obj) => throw null;
        public static bool IsInstanceOfException(MethodTable* pTargetType, object obj) => throw null;
    }

    internal static unsafe class RuntimeExports
    {
        public static object RhBox(MethodTable* pMT, ref byte data) => throw null;
        public static object RhUnbox2(MethodTable* pMT, object obj, ref bool pUnboxed) => throw null;
        public static object RhUnboxNullable(MethodTable* pMT, object obj, ref byte data) => throw null;
        public static object RhUnboxTypeTest(object obj) => throw null;
    }
}

namespace System.Threading
{
    internal static class Monitor
    {
        public static void SynchronizedMethodEnter(object obj) => throw null;
        public static void SynchronizedMethodExit(object obj) => throw null;
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    internal static class StartupCodeHelpers
    {
        [System.Runtime.RuntimeExport("RhpReversePInvoke")]
        static void RhpReversePInvoke(IntPtr frame) { }
        [System.Runtime.RuntimeExport("RhpReversePInvokeReturn")]
        static void RhpReversePInvokeReturn(IntPtr frame) { }
        [System.Runtime.RuntimeExport("RhpPInvoke")]
        static void RhpPInvoke(IntPtr frame) { }
        [System.Runtime.RuntimeExport("RhpPInvokeReturn")]
        static void RhpPInvokeReturn(IntPtr frame) { }
        [System.Runtime.RuntimeExport("RhpFallbackFailFast")]
        static void RhpFallbackFailFast() { while (true) { } }
    }

    internal static class ThrowHelpers
    {
        static void Fail() { while (true) { } }
        internal static void ThrowBodyRemoved() => Fail();
        internal static void ThrowFeatureBodyRemoved() => Fail();
        internal static void ThrowInstanceBodyRemoved() => Fail();
        internal static void ThrowUnavailableType() => Fail();
        internal static void ThrowBadImageFormatException(ExceptionStringID id) => Fail();
        internal static void ThrowTypeLoadException(ExceptionStringID id, string className, string typeName) => Fail();
        internal static void ThrowTypeLoadExceptionWithArgument(ExceptionStringID id, string className, string typeName, string messageArg) => Fail();
        internal static void ThrowMissingMethodException(ExceptionStringID id, string methodName) => Fail();
        internal static void ThrowMissingFieldException(ExceptionStringID id, string fieldName) => Fail();
        internal static void ThrowFileNotFoundException(ExceptionStringID id, string fileName) => Fail();
        internal static void ThrowInvalidProgramException(ExceptionStringID id) => Fail();
        internal static void ThrowInvalidProgramExceptionWithArgument(ExceptionStringID id, string methodName) => Fail();
        internal static void ThrowMarshalDirectiveException(ExceptionStringID id) => Fail();
        internal static void ThrowAmbiguousMatchException(ExceptionStringID id) => Fail();
        internal static void ThrowNotSupportedInlineArrayEqualsGetHashCode() => Fail();
    }
}
