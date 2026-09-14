# Microsoft Copilot assistance

Microsoft Copilot was used during this API phase to:

- Scaffold the ASP.NET Core minimal API project and its `Program.cs` boilerplate,
  including service registration, the application builder, OpenAPI support, and
  the HTTP pipeline.
- Generate the initial CRUD endpoint structure for listing users, retrieving a
  user by ID, creating a user, updating a user, and deleting a user.
- Improve the generated implementation with an in-memory `UserStore`, thread-safe
  storage, sequential IDs, and `201 Created` responses containing a `Location`
  header.
- Add request validation for required names and valid email addresses, duplicate
  email conflict responses, and consistent `404 Not Found` handling.
- Produce the `UserManagementAPI.http` request collection used to exercise every
  CRUD operation.

The current store is intentionally in memory for this project phase. Data is
reset whenever the API process restarts; a database-backed repository should be
introduced before production use.

## CRUD test procedure

Run the API with `dotnet run`, then execute the requests in
`UserManagementAPI.http` (or import equivalent requests into Postman) in this
order:

1. `GET /api/users` returns an empty list initially.
2. `POST /api/users` creates a user and returns `201 Created`.
3. `GET /api/users/1` returns the created user.
4. `PUT /api/users/1` updates the user and returns `200 OK`.
5. `DELETE /api/users/1` returns `204 No Content`.
6. `GET /api/users/1` now returns `404 Not Found`.

Invalid email/name data returns `400 Bad Request`, duplicate email addresses
return `409 Conflict`, and unknown IDs return `404 Not Found`.
