using IdleHeroDefense.Editor;
using NUnit.Framework;

namespace IdleHeroDefense.Tests
{
    public sealed class ProjectValidatorTests
    {
        [Test]
        public void ProjectContent_PassesReleaseValidation()
        {
            Assert.DoesNotThrow(ProjectValidator.ValidateOrThrow);
        }
    }
}

