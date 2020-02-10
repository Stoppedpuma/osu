﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Game.Rulesets;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osu.Framework.Allocation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Rankings.Tables;
using System.Linq;
using osu.Game.Overlays.Direct;
using System.Threading;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Overlays.Rankings
{
    public class SpotlightsLayout : CompositeDrawable
    {
        public readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

        private readonly Bindable<APISpotlight> selectedSpotlight = new Bindable<APISpotlight>();

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        private CancellationTokenSource cancellationToken;
        private GetSpotlightRankingsRequest getRankingsRequest;
        private GetSpotlightsRequest spotlightsRequest;

        private readonly SpotlightSelector selector;
        private readonly Container content;
        private readonly DimmedLoadingLayer loading;

        public SpotlightsLayout()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChild = new ReverseChildIDFillFlowContainer<Drawable>
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 20),
                Children = new Drawable[]
                {
                    selector = new SpotlightSelector
                    {
                        Current = selectedSpotlight,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            content = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                            loading = new DimmedLoadingLayer(),
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedSpotlight.BindValueChanged(onSpotlightChanged);
            Ruleset.BindValueChanged(onRulesetChanged);

            getSpotlights();
        }

        private void getSpotlights()
        {
            spotlightsRequest = new GetSpotlightsRequest();
            spotlightsRequest.Success += response => selector.Spotlights = response.Spotlights;
            api.Queue(spotlightsRequest);
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> ruleset)
        {
            if (!selector.Spotlights.Any())
                return;

            selectedSpotlight.TriggerChange();
        }

        private void onSpotlightChanged(ValueChangedEvent<APISpotlight> spotlight)
        {
            loading.Show();

            cancellationToken?.Cancel();
            getRankingsRequest?.Cancel();

            getRankingsRequest = new GetSpotlightRankingsRequest(Ruleset.Value, spotlight.NewValue.Id);
            getRankingsRequest.Success += onSuccess;
            api.Queue(getRankingsRequest);
        }

        private void onSuccess(GetSpotlightRankingsResponse response)
        {
            LoadComponentAsync(createContent(response), loaded =>
            {
                selector.ShowInfo(response);

                content.Clear();
                content.Add(loaded);

                loading.Hide();
            }, (cancellationToken = new CancellationTokenSource()).Token);
        }

        private Drawable createContent(GetSpotlightRankingsResponse response) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Y,
            RelativeSizeAxes = Axes.X,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 20),
            Children = new Drawable[]
            {
                new ScoresTable(1, response.Users),
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Spacing = new Vector2(10),
                    Children = response.BeatmapSets.Select(b => new DirectGridPanel(b.ToBeatmapSet(rulesets))
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    }).ToList()
                }
            }
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            spotlightsRequest?.Cancel();
            getRankingsRequest?.Cancel();
            cancellationToken?.Cancel();
        }
    }
}
