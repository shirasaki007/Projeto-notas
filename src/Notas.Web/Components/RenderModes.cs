using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Notas.Web.Components;

public static class RenderModes
{
    /// <summary>Sem prerender: o workspace só carrega dados uma vez, já autenticado no circuito.</summary>
    public static readonly IComponentRenderMode Interactive = new InteractiveServerRenderMode(prerender: false);
}
