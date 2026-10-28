import { createApiClient, type ApiClient } from './api';
import { archives, collections, createLibraryServices, prompts, templates, type CollectionClient, type LibraryArchiveClient, type PromptLibraryClient, type TemplateClient } from './library';
import { createLiveClient, createVisibleSessionClient, live, observeOperation, releaseSurface, startVisible, surfaceCapability, type LiveClient, type LiveEvents, type VisibleSessionClient } from './live';
import { createPreparationClient, preparation, type PreparationClient } from './preparation';
import { createProviderClient, providers, type ProviderClient } from './providers';
import { createPreferenceClient, preferences, type PreferenceClient } from './preferences';
import { createHistoryClient, history, type CompilationHistoryClient } from './history';

// Connection composition only. No React, task state, DOM work or domain authority.
export interface ApplicationServices {
  history: CompilationHistoryClient;
  prompts: PromptLibraryClient;
  templates: TemplateClient;
  collections: CollectionClient;
  archives: LibraryArchiveClient;
  preparation: PreparationClient;
  live: LiveClient;
  visibleSession: VisibleSessionClient;
  providers: ProviderClient;
  preferences: PreferenceClient;
}
export function createApplicationServices(client: ApiClient = createApiClient(), eventSource?: (url: string) => LiveEvents): ApplicationServices {
  const liveClient = createLiveClient(client, eventSource);
  return {
    history: createHistoryClient(client),
    ...createLibraryServices(client),
    preparation: createPreparationClient(client),
    live: liveClient,
    visibleSession: createVisibleSessionClient(liveClient),
    providers: createProviderClient(client),
    preferences: createPreferenceClient(client),
  };
}

// Standalone consumers retain their existing bindings and resolve only ports they use.
const compatibleVisibleSession: VisibleSessionClient = {
  startVisible: (...args) => startVisible(...args), observeOperation: (...args) => observeOperation(...args),
  surfaceCapability: id => surfaceCapability(id), releaseSurface: id => releaseSurface(id),
};
export const defaultApplicationServices: ApplicationServices = {
  get history() { return history; },
  get prompts() { return prompts; }, get templates() { return templates; },
  get collections() { return collections; }, get archives() { return archives; },
  get preparation() { return preparation; }, get live() { return live; },
  visibleSession: compatibleVisibleSession,
  get providers() { return providers; }, get preferences() { return preferences; },
};
