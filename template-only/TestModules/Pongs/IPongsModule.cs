using Tsikhanau.Monolith.Application.Modules;
using Tsikhanau.Monolith.TestModules.Pongs.Contracts;

namespace Tsikhanau.Monolith.TestModules.Pongs;

internal interface IPongsModule : IPongsQueryFacade, IModuleFacade;
