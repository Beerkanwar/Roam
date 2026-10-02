using Roam.Domain.Users;
using Xunit;

namespace Roam.Domain.Tests.Users;

public class ApplicationUserTests
{
    [Fact]
    public void NewUser_HasCorrectDefaults()
    {
        // Act
        var user = new ApplicationUser();

        // Assert
        Assert.Equal(AccountStatus.Active, user.AccountStatus);
        Assert.Equal(VolunteerStatus.None, user.VolunteerStatus);
        Assert.Equal(AgeCategory.Unknown, user.AgeCategory);
        Assert.Null(user.DateOfBirth);
    }
}
