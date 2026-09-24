export interface MenuItem {
  label: string;
  icon: string;
  route?: string;
  /** F-Authorization permission required to see this item. Undefined = visible to everyone signed in. */
  permission?: string;
  /** F-Features flag that must be on for this item to show. */
  feature?: string;
  /** A group (FR-SHELL-002): renders as a heading with these entries beneath it and hides itself when none are visible. */
  children?: MenuItem[];
  /** Who contributed the item (e.g. "plugins"), so a whole contribution can be replaced or removed at runtime (FR-SHELL-008). */
  source?: string;
}
