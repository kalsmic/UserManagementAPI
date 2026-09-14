using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapGet("/api/users", (UserStore store) =>
    Results.Ok(store.GetAll()))
    .WithName("GetUsers");

app.MapGet("/api/users/{id:int}", (int id, UserStore store) =>
{
    var user = store.Get(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
})
.WithName("GetUserById");

app.MapPost("/api/users", (UserRequest? request, UserStore store) =>
{
    var validation = Validate(request);
    if (validation is not null || request is null)
    {
        return Results.ValidationProblem(validation ?? new Dictionary<string, string[]>
        {
            ["request"] = ["A request body is required."]
        });
    }

    if (!store.TryAdd(request, out var user))
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    return Results.Created($"/api/users/{user.Id}", user);
})
.WithName("CreateUser");

app.MapPut("/api/users/{id:int}", (int id, UserRequest? request, UserStore store) =>
{
    var validation = Validate(request);
    if (validation is not null || request is null)
    {
        return Results.ValidationProblem(validation ?? new Dictionary<string, string[]>
        {
            ["request"] = ["A request body is required."]
        });
    }

    var result = store.TryUpdate(id, request, out var user);
    if (result == UpdateResult.NotFound)
    {
        return Results.NotFound();
    }

    if (result == UpdateResult.DuplicateEmail)
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    return Results.Ok(user);
})
.WithName("UpdateUser");

app.MapDelete("/api/users/{id:int}", (int id, UserStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteUser");

app.Run();

static Dictionary<string, string[]>? Validate(UserRequest? request)
{
    var errors = new Dictionary<string, string[]>();

    if (request is null)
    {
        return new Dictionary<string, string[]>
        {
            ["request"] = ["A request body is required."]
        };
    }

    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
    {
        errors["name"] = ["Name is required and must be 200 characters or fewer."];
    }

    if (string.IsNullOrWhiteSpace(request.Email) ||
        request.Email.Trim().Length > 320 ||
        !new EmailAddressAttribute().IsValid(request.Email.Trim()))
    {
        errors["email"] = ["A valid email address of 320 characters or fewer is required."];
    }

    return errors.Count == 0 ? null : errors;
}

public sealed record User(int Id, string Name, string Email);

public sealed record UserRequest(string Name, string Email);

public sealed class UserStore
{
    private readonly ConcurrentDictionary<int, User> users = new();
    private readonly object sync = new();
    private User[] orderedSnapshot = [];
    private int nextId;

    public IReadOnlyCollection<User> GetAll() => Volatile.Read(ref orderedSnapshot);

    public User? Get(int id) =>
        users.TryGetValue(id, out var user) ? user : null;

    public bool TryAdd(UserRequest request, out User user)
    {
        lock (sync)
        {
            if (users.Values.Any(existing => string.Equals(existing.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                user = null!;
                return false;
            }

            user = new User(Interlocked.Increment(ref nextId), request.Name.Trim(), request.Email.Trim());
            users[user.Id] = user;
            RefreshSnapshot();
            return true;
        }
    }

    public UpdateResult TryUpdate(int id, UserRequest request, out User? user)
    {
        lock (sync)
        {
            if (!users.ContainsKey(id))
            {
                user = null;
                return UpdateResult.NotFound;
            }

            if (users.Values.Any(existing =>
                    existing.Id != id &&
                    string.Equals(existing.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                user = null;
                return UpdateResult.DuplicateEmail;
            }

            user = new User(id, request.Name.Trim(), request.Email.Trim());
            users[id] = user;
            RefreshSnapshot();
            return UpdateResult.Updated;
        }
    }

    public bool Delete(int id)
    {
        lock (sync)
        {
            if (!users.TryRemove(id, out _))
            {
                return false;
            }

            RefreshSnapshot();
            return true;
        }
    }

    private void RefreshSnapshot() =>
        Volatile.Write(ref orderedSnapshot, users.Values.OrderBy(user => user.Id).ToArray());
}

public enum UpdateResult
{
    Updated,
    NotFound,
    DuplicateEmail
}
