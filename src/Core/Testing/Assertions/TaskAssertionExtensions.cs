using Shouldly;

namespace Tsikhanau.Monolith.Testing.Assertions;

public static class TaskAssertionExtensions
{
    extension(Task task)
    {
        public async Task ShouldRunToCompletionAsync()
        {
            await Should.NotThrowAsync(task);
            task.Status.ShouldBe(TaskStatus.RanToCompletion);
        }
    }
}
