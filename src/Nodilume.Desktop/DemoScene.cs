namespace Nodilume.Desktop;

internal static class DemoScene
{
    public static object Create()
    {
        var nodes = new List<object>();
        var links = new List<object>();
        nodes.Add(new { id = "root", title = "Idee connesse", x = 0d, y = 0d, z = 0d, color = "#f4c676" });
        string[][] titles = [
            ["Conoscenza", "Domande", "Fonti", "Evidenze", "Interpretazioni", "Memoria", "Apprendimento", "Sintesi"],
            ["Progetti", "Intuizioni", "Obiettivi", "Esperimenti", "Vincoli", "Decisioni", "Prototipi", "Risultati"],
            ["Connessioni", "Analogie", "Contrasti", "Cause", "Conseguenze", "Prospettive", "Contesti", "Possibilità"]
        ];
        string[] colors = ["#65d8bf", "#8caafa", "#db9aca"];
        double[][] centers = [[-115,35,0], [95,65,-45], [40,-95,35]];
        for (int group = 0; group < 3; group++)
        {
            var c = centers[group];
            for (int i = 0; i < 8; i++)
            {
                var angle = (i - 1) * Math.Tau / 7;
                var id = $"g{group}-{i}";
                nodes.Add(new { id, title = titles[group][i],
                    x = c[0] + (i == 0 ? 0 : Math.Cos(angle) * 48),
                    y = c[1] + (i == 0 ? 0 : Math.Sin(angle) * 48),
                    z = c[2] + (i == 0 ? 0 : Math.Sin(angle * 2) * 38), color = colors[group] });
                links.Add(new { source = i == 0 ? "root" : $"g{group}-0", target = id });
            }
        }
        links.Add(new { source = "g0-1", target = "g1-3" });
        links.Add(new { source = "g0-4", target = "g2-6" });
        links.Add(new { source = "g1-5", target = "g2-3" });
        return new { version = 1, type = "scene", mapId = "demo", revision = 1, nodes, links };
    }
}
