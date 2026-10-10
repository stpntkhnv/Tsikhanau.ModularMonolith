using Acme.Modules.Peer.Contracts;
using Tsikhanau.Monolith.Application.Modules;

namespace Acme.Modules.Peer;

internal interface IPeerModule : IPeerQueryFacade, IModuleFacade;
