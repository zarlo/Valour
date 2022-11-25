﻿using System.Text.Json.Serialization;

namespace Valour.Api.Items.Messages.Embeds.Styles.Basic;

public class BackgroundColor : StyleBase
{
    [JsonPropertyName("c")]
    public Color Color { get; set; }

    public BackgroundColor(Color color)
    {
        Color = color;
    }

    public override string ToString()
    {
        return $"background-color: {Color};";
    }
}
