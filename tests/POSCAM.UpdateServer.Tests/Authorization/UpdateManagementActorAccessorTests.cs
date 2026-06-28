using POSCAM.UpdateServer.Api.Authorization;
using POSCAM.UpdateServer.Api.Models.Authorization;

namespace POSCAM.UpdateServer.Tests.Authorization;

public class UpdateManagementActorAccessorTests
{
    [Fact]
    public void SetActor_현재요청의_Actor를_저장한다()
    {
        var accessor = new UpdateManagementActorAccessor();
        var actor = new UpdateManagementActor
        {
            UserCode = 1,
            UserName = "시스템 관리자",
            UserRole = 0
        };

        accessor.SetActor(actor);

        Assert.Same(actor, accessor.Actor);
    }

    [Fact]
    public void SetActor_같은요청에서_두번설정하면_거부한다()
    {
        var accessor = new UpdateManagementActorAccessor();
        accessor.SetActor(new UpdateManagementActor
        {
            UserCode = 1,
            UserName = "시스템 관리자",
            UserRole = 0
        });

        Assert.Throws<InvalidOperationException>(
            () => accessor.SetActor(new UpdateManagementActor
            {
                UserCode = 2,
                UserName = "다른 관리자",
                UserRole = 1
            }));
    }
}
