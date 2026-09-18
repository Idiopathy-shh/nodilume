using Nodilume.Core;

namespace Nodilume.Application;

public static class DemoMapFactory
{
    private static Guid G(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static IdeaId I(int value) => new(G(value));
    private static PlacementId P(int value) => new(G(value));
    private static RelationId R(int value) => new(G(value));

    public static MapGraph Create()
    {
        var mapId = new MapId(G(1));
        var graph = new MapGraph(new MapInfo(mapId, "Mappa dimostrativa", 0, 1));
        var rootIdea = I(1000);
        graph.AddIdea(rootIdea, "Idee connesse", "Radice della mappa dimostrativa.");
        graph.AddPlacement(P(2000), rootIdea, null, 0, 0, 0, annotation: "Radice della demo");

        string[][] titles =
        [
            ["Conoscenza", "Domande", "Fonti", "Evidenze", "Interpretazioni", "Memoria", "Apprendimento", "Sintesi"],
            ["Progetti", "Intuizioni", "Obiettivi", "Esperimenti", "Vincoli", "Decisioni", "Prototipi", "Risultati"],
            ["Connessioni", "Analogie", "Contrasti", "Cause", "Conseguenze", "Prospettive", "Contesti", "Possibilità"]
        ];
        double[][] centers = [[-115, 35, 0], [95, 65, -45], [40, -95, 35]];

        for (var group = 0; group < 3; group++)
        {
            for (var i = 0; i < 8; i++)
            {
                var slot = group * 8 + i;
                var placementId = P(2100 + slot);
                var ideaId = group == 2 && i == 7 ? I(1101) : I(1100 + slot);
                if (!graph.Ideas.ContainsKey(ideaId))
                {
                    var title = titles[group][i];
                    graph.AddIdea(ideaId, title, $"Contenuto condiviso di {title}.");
                }

                var parentId = i == 0 ? P(2000) : P(2100 + group * 8);
                double x;
                double y;
                double z;
                if (i == 0)
                {
                    x = centers[group][0];
                    y = centers[group][1];
                    z = centers[group][2];
                }
                else
                {
                    var angle = (i - 1) * Math.Tau / 7;
                    x = Math.Cos(angle) * 48;
                    y = Math.Sin(angle) * 48;
                    z = Math.Sin(angle * 2) * 38;
                }

                var ideaTitle = graph.Ideas[ideaId].Title;
                graph.AddPlacement(
                    placementId,
                    ideaId,
                    parentId,
                    x,
                    y,
                    z,
                    annotation: $"{ideaTitle} nel contesto {titles[group][0]}");
            }
        }

        graph.AddRelation(R(3001), I(1101), I(1111), "cross", true, "Domande verso Esperimenti");
        graph.AddRelation(R(3002), I(1104), I(1122), "cross", true, "Interpretazioni verso Contesti");
        graph.AddRelation(R(3003), I(1113), I(1119), "cross", true, "Decisioni verso Cause");
        return graph;
    }
}