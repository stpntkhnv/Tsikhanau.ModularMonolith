using Shouldly;

namespace Tsikhanau.Monolith.Testing.Assertions;

public static class TaskAssertionExtensions
{
    public static async Task ShouldRunToCompletionAsync(this Task task)
    {
        await Should.NotThrowAsync(task);
        task.Status.ShouldBe(TaskStatus.RanToCompletion);
    }
}
