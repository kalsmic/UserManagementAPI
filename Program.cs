using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

app.MapPost("/api/users", (UserRequest request, UserStore store) =>
{
    var validation = Validate(request);
    if (validation is not null)
    {
        return Results.ValidationProblem(validation);
    }

    if (store.EmailExists(request.Email))
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    var user = store.Add(request);
    return Results.Created($"/api/users/{user.Id}", user);
})
.WithName("CreateUser");

app.MapPut("/api/users/{id:int}", (int id, UserRequest request, UserStore store) =>
{
    var validation = Validate(request);
    if (validation is not null)
    {
        return Results.ValidationProblem(validation);
    }

    if (!store.Exists(id))
    {
        return Results.NotFound();
    }

    if (store.EmailExists(request.Email, id))
    {
        return Results.Conflict(new { message = "A user with this email already exists." });
    }

    return Results.Ok(store.Update(id, request));
})
.WithName("UpdateUser");

app.MapDelete("/api/users/{id:int}", (int id, UserStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteUser");

app.Run();

static Dictionary<string, string[]>? Validate(UserRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors["name"] = ["Name is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Email) ||
        !new EmailAddressAttribute().IsValid(request.Email))
    {
        errors["email"] = ["A valid email address is required."];
    }

    return errors.Count == 0 ? null : errors;
}

public sealed record User(int Id, string Name, string Email);

public sealed record UserRequest(string Name, string Email);

public sealed class UserStore
{
    private readonly ConcurrentDictionary<int, User> users = new();
    private int nextId;

    public IReadOnlyCollection<User> GetAll() =>
        users.Values.OrderBy(user => user.Id).ToArray();

    public User? Get(int id) =>
        users.TryGetValue(id, out var user) ? user : null;

    public bool Exists(int id) => users.ContainsKey(id);

    public bool EmailExists(string email, int? excludedId = null) =>
        users.Values.Any(user =>
            (!excludedId.HasValue || user.Id != excludedId.Value) &&
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

    public User Add(UserRequest request)
    {
        var user = new User(Interlocked.Increment(ref nextId), request.Name.Trim(), request.Email.Trim());
        users[user.Id] = user;
        return user;
    }

    public User Update(int id, UserRequest request)
    {
        var user = new User(id, request.Name.Trim(), request.Email.Trim());
        users[id] = user;
        return user;
    }

    public bool Delete(int id) => users.TryRemove(id, out _);
}
