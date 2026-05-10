using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCTVocabulary.Models;

var services = new ServiceCollection();
var connectionString = "Server=localhost;Database=nhtctsnn_tctenglish;User=root;Password=;"; // I don't know the password, this might fail.
// Better: Use the Program.cs pattern to get services from the app.
