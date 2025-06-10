using UserManagementApi.Models;
using UserManagementApi.Services;

namespace UserManagementApi.Endpoints;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app) {
        var group = app.MapGroup("/api/users")
               .WithTags("Users")
               .WithOpenApi();

        // apply rate limiting to every endpoint in this group
        group.RequireRateLimiting("Global").RequireAuthorization();
        // GET api/users
        group.MapGet("/", async (IUserService service) => {
            var users = await service.GetAllAsync();
            //.RequireAuthorization()
            //.CacheOutput("Short30s");
            return Results.Ok(users);
        })
        .WithName("GetAllUsers")
        .Produces<IEnumerable<User>>(StatusCodes.Status200OK)
        .WithSummary("Retrieves all users")
        .WithDescription("Returns a JSON array of all users in the system, including associated Car details");

        // GET api/users/{id}
        group.MapGet("/{id:int}", async (int id, IUserService service) => {
            var user = await service.GetByIdAsync(id);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        })
        .WithName("GetUserById")
        .Produces<User>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Retrieves a specific user by ID")
        .WithDescription("Returns the user object (including associated Car) for the given user ID.");

        //group.MapPost("/", async (User newUser, IUserService service) => {
        //    var created = await service.CreateAsync(newUser);
        //    return Results.Created($"/api/users/{created.Id}", created);
        //})
        group.MapPost("/", async (
        User newUser,
        HttpRequest req,
        IUserService userSvc,
        IIdempotencyService idemSvc) =>
        {
            // 1) Read & validate Idempotency-Key
            if (!req.Headers.TryGetValue("Idempotency-Key", out var header)
                || string.IsNullOrWhiteSpace(header.First())) {
                return Results.BadRequest("Missing or empty Idempotency-Key header");
            }
            var key = header.First();

            const string op = "CreateUser";

            // 2) Check for an existing record
            var existingId = await idemSvc.GetResourceIdAsync(key, op);
            if (existingId.HasValue) {
                var existing = await userSvc.GetByIdAsync(existingId.Value);
                if (existing is null)
                    return Results.NotFound();
                return Results.Created($"/users/{existing.Id}", existing);
            }

            // 3) No previous run: create a new user
            var created = await userSvc.CreateAsync(newUser);

            // 4) Save the idempotency record
            await idemSvc.SaveResourceIdAsync(key, op, created.Id);

            // 5) Return the usual 201 Created
            return Results.Created($"/users/{created.Id}", created);
        })
        .WithName("CreateUser")
        .Accepts<User>("application/json")
        .Produces<User>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithSummary("Creates a new user")
        .WithDescription("Creates a new user with Name, Email, Password, and optional CarId. Returns the created object.");

        // PUT api/users/{id}
        group.MapPut("/{id:int}", async (int id, User updatedUser, IUserService service) => {
            var success = await service.UpdateAsync(id, updatedUser);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UpdateUser")
        .Accepts<User>("application/json")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .WithSummary("Updates an existing user")
        .WithDescription("Updates the fields Name, Email, Password, and CarId for an existing user. Returns 204 if successful or 404 if not found.");

        // DELETE api/users/{id}
        group.MapDelete("/{id:int}", async (int id, IUserService service) => {
            var success = await service.DeleteAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithSummary("Deletes a user by ID")
        .WithDescription("Removes the user with the given ID from the database. Returns 204 if deleted or 404 if not found.");

        return app;
    }
}
