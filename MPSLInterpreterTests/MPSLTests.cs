using MPSLInterpreter;

namespace MPSLInterpreterTests;

public class MPSLTests
{
    [Test]
    public void TestRunEmpty()
    {
        MPSLRunResult result = MPSL.Run("", new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.TokenizerErrors, Is.Empty);
            Assert.That(result.ParserErrors, Is.Empty);
        });
    }

    [Test]
    public void TestRunCode()
    {
        MPSLRunResult result = MPSL.Run("0 -> var i", new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.TokenizerErrors, Is.Empty);
            Assert.That(result.ParserErrors, Is.Empty);
        });
    }

    [Test]
    public void TestRunTokenizerError()
    {
        MPSLRunResult result = MPSL.Run("╚ -> var i", new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.TokenizerErrors, Has.Count.EqualTo(1));
            Assert.That(result.ParserErrors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TestRunParserError()
    {
        MPSLRunResult result = MPSL.Run("\"test\" -> var", new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.TokenizerErrors, Is.Empty);
            Assert.That(result.ParserErrors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TestRunFileError()
    {
        MPSLRunResult result = MPSL.RunFile("", new());
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.TokenizerErrors, Is.Empty);
            Assert.That(result.ParserErrors, Is.Empty);
        });
    }
}