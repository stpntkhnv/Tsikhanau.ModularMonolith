using Acme.Modules.Peer.Contracts;
using Tsikhanau.Monolith.Application.Modules;

namespace Acme.Modules.Peer;

public interface IPeerModule : IPeerQueryFacade, IModuleFacade;
