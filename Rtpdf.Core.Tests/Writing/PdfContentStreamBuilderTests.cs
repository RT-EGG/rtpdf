using System.Text;
using Rtpdf.Core.Content;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Writing.Content;
using Xunit;

namespace Rtpdf.Core.Tests.Writing;

public sealed class PdfContentStreamBuilderTests
{
    [Fact]
    public void BuildsImagePlacementOperationsThatRoundTripThroughTheParser()
    {
        var builder = new PdfContentStreamBuilder()
            .SaveGraphicsState()
            .ConcatenateMatrix(200, 0, 0, 100, 10.5m, 20)
            .DrawXObject("Image1")
            .RestoreGraphicsState();

        var stream = builder.Build();
        var text = Encoding.ASCII.GetString(stream.EncodedData.Span);
        var operations = new PdfContentStreamParser(stream.EncodedData).Parse();

        Assert.Equal(
            "q\n200 0 0 100 10.5 20 cm\n/Image1 Do\nQ\n",
            text);
        Assert.Collection(
            operations,
            operation => Assert.Equal("q", operation.Operator),
            operation =>
            {
                Assert.Equal("cm", operation.Operator);
                Assert.Equal(6, operation.Operands.Count);
                Assert.Equal(new PdfReal(10.5m), operation.Operands[4]);
            },
            operation =>
            {
                Assert.Equal("Do", operation.Operator);
                Assert.Equal(new PdfName("Image1"), Assert.Single(operation.Operands));
            },
            operation => Assert.Equal("Q", operation.Operator));
        Assert.Equal(4, builder.OperationCount);
        Assert.Equal(text.Length, builder.Length);
    }

    [Fact]
    public void AppendsCustomOperationsWithCompositeOperandsAsSnapshots()
    {
        var array = new PdfArray { new PdfInteger(1), new PdfString("A"u8) };
        var dictionary = new PdfDictionary { ["Enabled"] = PdfBoolean.True };
        var builder = new PdfContentStreamBuilder()
            .AppendOperation("VendorOp", array, dictionary);
        array.Add(new PdfInteger(2));
        dictionary["Enabled"] = PdfBoolean.False;

        var operation = Assert.Single(
            new PdfContentStreamParser(builder.GetContentBytes()).Parse());

        Assert.Equal("VendorOp", operation.Operator);
        Assert.Equal(2, Assert.IsType<PdfArray>(operation.Operands[0]).Count);
        Assert.Equal(
            PdfBoolean.True,
            Assert.IsType<PdfDictionary>(operation.Operands[1])["Enabled"]);
    }

    [Fact]
    public void RequiresBalancedGraphicsStateOperationsAtBuildTime()
    {
        var builder = new PdfContentStreamBuilder();

        Assert.Throws<PdfWriteException>(() => builder.RestoreGraphicsState());
        Assert.Equal(0, builder.OperationCount);

        builder.SaveGraphicsState();
        Assert.Equal(1, builder.GraphicsStateDepth);
        Assert.Throws<PdfWriteException>(() => builder.Build());

        builder.RestoreGraphicsState();
        Assert.Equal(0, builder.GraphicsStateDepth);
        Assert.NotNull(builder.Build());
    }

    [Fact]
    public void RejectsAnEmptyOperatorName()
    {
        var builder = new PdfContentStreamBuilder();

        Assert.Throws<ArgumentException>(() => builder.AppendOperation(""));
        Assert.Equal(0, builder.OperationCount);
    }

    [Theory]
    [InlineData("bad operator")]
    [InlineData("/Name")]
    [InlineData("BI")]
    [InlineData("ID")]
    [InlineData("EI")]
    [InlineData("日本語")]
    public void RejectsInvalidOrUnsupportedOperators(string operatorName)
    {
        var builder = new PdfContentStreamBuilder();

        Assert.Throws<PdfWriteException>(() => builder.AppendOperation(operatorName));
        Assert.Equal(0, builder.OperationCount);
    }

    [Fact]
    public void RejectsIndirectStreamAndCyclicOperands()
    {
        var builder = new PdfContentStreamBuilder();
        Assert.Throws<PdfWriteException>(() =>
            builder.AppendOperation("op", new PdfIndirectReference(1)));
        Assert.Throws<PdfWriteException>(() =>
            builder.AppendOperation("op", new PdfStream(new PdfDictionary(), [])));

        var nestedReference = new PdfArray { new PdfIndirectReference(1) };
        Assert.Throws<PdfWriteException>(() => builder.AppendOperation("op", nestedReference));

        var cyclic = new PdfArray();
        cyclic.Add(cyclic);
        Assert.Throws<PdfWriteException>(() => builder.AppendOperation("op", cyclic));
        Assert.Equal(0, builder.OperationCount);
        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void EnforcesConfiguredOperationOperandAndLengthLimitsAtomically()
    {
        var operationLimit = new PdfContentStreamBuilder(
            new PdfContentStreamBuilderOptions { MaximumOperations = 1 });
        operationLimit.AppendOperation("q");
        Assert.Throws<PdfWriteException>(() => operationLimit.AppendOperation("Q"));

        var operandLimit = new PdfContentStreamBuilder(
            new PdfContentStreamBuilderOptions { MaximumOperandsPerOperation = 1 });
        Assert.Throws<PdfWriteException>(() => operandLimit.AppendOperation(
            "m",
            new PdfInteger(1),
            new PdfInteger(2)));

        var lengthLimit = new PdfContentStreamBuilder(
            new PdfContentStreamBuilderOptions { MaximumContentLength = 1 });
        Assert.Throws<PdfWriteException>(() => lengthLimit.AppendOperation("q"));
        Assert.Equal(0, lengthLimit.OperationCount);
        Assert.Equal(0, lengthLimit.Length);
    }

    [Fact]
    public void EnforcesOperatorAndObjectNestingLimits()
    {
        var operatorLimit = new PdfContentStreamBuilder(
            new PdfContentStreamBuilderOptions { MaximumOperatorLength = 1 });
        Assert.Throws<PdfWriteException>(() => operatorLimit.AppendOperation("cm"));

        var nestingLimit = new PdfContentStreamBuilder(
            new PdfContentStreamBuilderOptions { MaximumObjectNestingDepth = 1 });
        var nested = new PdfArray { new PdfArray { new PdfArray() } };
        Assert.Throws<PdfWriteException>(() => nestingLimit.AppendOperation("op", nested));
    }
}
