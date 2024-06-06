using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

// Añadir un servicio singleton de ITaskService usando una implementación en memoria.
builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());

var app = builder.Build();

// Configurar una opción de redirección de URLs para redirigir de "tasks/(.*)" a "todos/$1".
var rewriteOptions = new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1");
app.UseRewriter(rewriteOptions);

// Middleware para registrar el inicio y fin de cada petición HTTP.
app.Use(async (context, next) => {
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();

// Definir la ruta para obtener todos los todos.
app.MapGet("/todos", (ITaskService service) => service.GetTodos());

// Definir la ruta para obtener un todo por su id.
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) => {
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null
    ? TypedResults.NotFound() // Devuelve 404 si no se encuentra el todo.
    : TypedResults.Ok(targetTodo); // Devuelve 200 y el todo si se encuentra.
});

// Definir la ruta para agregar un nuevo todo.
app.MapPost("/todos", (Todo task, ITaskService service) => {
    service.AddTodo(task);
    return TypedResults.Created($"/todos/{task.Id}", task); // Devuelve 201 y la ubicación del nuevo todo.
})
// Filtro de endpoint para validar el todo antes de agregarlo.
.AddEndpointFilter(async (context, next) => {
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if (taskArgument.DueDate < DateTime.UtcNow) errors.Add(nameof(Todo.DueDate), ["cannot have due date in the past"]); // Verifica que la fecha de vencimiento no esté en el pasado.
    if (taskArgument.IsCompleted) errors.Add(nameof(Todo.IsCompleted), ["cannot add completed todo"]); // Verifica que el todo no esté completado al ser creado.
    if (errors.Count > 0) return Results.ValidationProblem(errors); // Si hay errores, devuelve un problema de validación.
    return await next(context);
});

// Definir la ruta para eliminar un todo por su id.
app.MapDelete("/todos/{id}", (int id, ITaskService service) => {
    service.DeleteTodoById(id);
    return TypedResults.NoContent(); // Devuelve 204 No Content.
});

app.Run();

// Definición del record Todo.
public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

// Definición de la interfaz ITaskService.
interface ITaskService {
    Todo? GetTodoById(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo task);
}

// Implementación en memoria de ITaskService.
class InMemoryTaskService : ITaskService 
{
    private readonly List<Todo> _todos = [];
    public Todo AddTodo(Todo task){
        _todos.Add(task);
        return task;
    }
    public void DeleteTodoById (int id) {
        _todos.RemoveAll((task) => id == task.Id);
    }
    public Todo? GetTodoById(int id) {
        return _todos.SingleOrDefault((t) => id == t.Id);
    }
    public List<Todo> GetTodos() {
        return _todos;
    }
}
