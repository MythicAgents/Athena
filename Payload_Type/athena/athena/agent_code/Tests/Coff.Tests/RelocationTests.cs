using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agent.Tests.CoffTests
{
    [TestClass]
    public class RelocationTests
    {
        private static MethodInfo GetCalculateRel32()
        {
            Type? coffType = Assembly.Load("coff").GetType("Agent.Coff");
            Assert.IsNotNull(coffType);

            MethodInfo? method = coffType.GetMethod(
                "CalculateRel32",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return method;
        }

        [TestMethod]
        public void CalculateRel32ReturnsSignedDisplacementForNearbyTarget()
        {
            MethodInfo method = GetCalculateRel32();

            object? result = method.Invoke(null, new object[] { 0x1000L, 0x1100L, 4 });

            Assert.AreEqual(0xFC, result);
        }

        [TestMethod]
        public void CalculateRel32RejectsTargetOutsideSigned32BitRange()
        {
            MethodInfo method = GetCalculateRel32();

            TargetInvocationException exception = Assert.ThrowsException<TargetInvocationException>(
                () => method.Invoke(null, new object[] { 0x1000L, 0x1_0000_1000L, 4 }));

            Assert.IsInstanceOfType<OverflowException>(exception.InnerException);
        }

        [TestMethod]
        public void IatUpdateWritesEveryCoffLocalImportReference()
        {
            Type? iatType = Assembly.Load("coff").GetType("Agent.IAT");
            Assert.IsNotNull(iatType);
            object? iat = Activator.CreateInstance(iatType, nonPublic: true);
            Assert.IsNotNull(iat);

            MethodInfo? add = iatType.GetMethod("Add");
            MethodInfo? addReference = iatType.GetMethod("AddReference");
            MethodInfo? update = iatType.GetMethod("Update");
            Assert.IsNotNull(add);
            Assert.IsNotNull(addReference);
            Assert.IsNotNull(update);

            IntPtr firstReference = Marshal.AllocHGlobal(IntPtr.Size);
            IntPtr secondReference = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                add.Invoke(iat, new object[] { "RunOF", "go", IntPtr.Zero });
                addReference.Invoke(iat, new object[] { "RunOF", "go", firstReference });
                addReference.Invoke(iat, new object[] { "RunOF", "go", secondReference });

                IntPtr target = new IntPtr(0x12345678);
                update.Invoke(iat, new object[] { "RunOF", "go", target });

                Assert.AreEqual(target, Marshal.ReadIntPtr(firstReference));
                Assert.AreEqual(target, Marshal.ReadIntPtr(secondReference));
            }
            finally
            {
                Marshal.FreeHGlobal(firstReference);
                Marshal.FreeHGlobal(secondReference);
            }
        }
    }
}
