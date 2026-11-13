using System.Reflection;
using System.Runtime.CompilerServices;
using FinSync.Domain;

namespace FinSync.Tests.Shared;

public class MaskingBoundaryTests
{
    [Fact]
    public void MaskedDescription_CanOnlyBeCreatedByTheMaskingModule()
    {
        var type = typeof(MaskedDescription);

        Assert.DoesNotContain(
            type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            constructor => constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly);
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.ReturnType == type);
        Assert.Null(type.GetProperty(nameof(MaskedDescription.Value))!.SetMethod);
        Assert.Equal(
            new[] { "FinSync.Shared", "FinSync.Tests" },
            type.Assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName).Order());
    }

    [Fact]
    public void MaskedDescription_EmptyValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MaskedDescription(null!));
        Assert.Throws<ArgumentException>(() => new MaskedDescription("   "));
    }
}
