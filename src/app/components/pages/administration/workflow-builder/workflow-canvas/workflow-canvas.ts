import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild } from '@angular/core';
import { CircleX, LucideAngularModule, Mail, Maximize, Minus, Network, Plus, Settings, Trash2, User, Zap } from 'lucide-angular';
import { NODE_TYPES } from '../workflow-builder.data';
import { NODE_LABELS, NODE_SIZE, NodeType, WfEdge, WfGraph, WfNode } from '../workflow-builder.model';

/** An edge resolved to an SVG path plus where its label sits. */
interface EdgeView {
  id: string;
  d: string;
  label: string;
  lx: number;
  ly: number;
  anchor: 'start' | 'middle' | 'end';
  rejected: boolean;
}

interface DragState {
  node: WfNode;
  startX: number;
  startY: number;
  origX: number;
  origY: number;
  moved: boolean;
}

const ZOOM_MIN = 0.5;
const ZOOM_MAX = 1.5;
const MINI_W = 150;
const MINI_H = 90;

/**
 * Workflow canvas: absolutely positioned node cards over an SVG connector layer.
 * Nodes can be dragged; a click without movement selects. The graph is mutated in place (positions only)
 * and `moved` tells the page the workflow has unsaved changes.
 */
@Component({
  standalone: true,
  selector: 'app-workflow-canvas',
  imports: [LucideAngularModule],
  templateUrl: './workflow-canvas.html',
  styleUrl: './workflow-canvas.css'
})
export class WorkflowCanvas {
  @Input({ required: true }) graph!: WfGraph;
  @Input() selectedId: string | null = null;
  @Input() readonly = false;
  @Output() select = new EventEmitter<string | null>();
  @Output() moved = new EventEmitter<void>();
  @Output() add = new EventEmitter<NodeType>();
  @Output() remove = new EventEmitter<void>();

  @ViewChild('viewport', { static: true }) viewport!: ElementRef<HTMLDivElement>;

  readonly icons = { Zap, Network, User, Settings, Mail, CircleX, Minus, Plus, Maximize, Trash2 };
  readonly nodeTypes = NODE_TYPES;
  readonly labels = NODE_LABELS;
  zoom = 1;
  addOpen = false;
  scrollLeft = 0;
  scrollTop = 0;
  private drag: DragState | null = null;

  // ===== Geometry =====

  size(node: WfNode): { w: number; h: number } {
    return NODE_SIZE[node.type];
  }

  /** Drawing area in canvas units (before zoom). */
  get content(): { w: number; h: number } {
    let w = 600;
    let h = 380;
    for (const node of this.graph.nodes) {
      const s = this.size(node);
      w = Math.max(w, node.x + s.w + 60);
      h = Math.max(h, node.y + s.h + 60);
    }
    return { w, h };
  }

  iconFor(type: NodeType) {
    switch (type) {
      case 'trigger': return this.icons.Zap;
      case 'condition': return this.icons.Network;
      case 'approval': return this.icons.User;
      case 'action': return this.icons.Settings;
      case 'notification': return this.icons.Mail;
      default: return this.icons.CircleX;
    }
  }

  edgeViews(): EdgeView[] {
    const byId = new Map(this.graph.nodes.map((node) => [node.id, node]));
    const views: EdgeView[] = [];
    for (const edge of this.graph.edges) {
      const s = byId.get(edge.from);
      const t = byId.get(edge.to);
      if (s && t) views.push(this.route(edge, s, t));
    }
    return views;
  }

  /** Orthogonal routing: straight across on the same row, otherwise down/up then across. */
  private route(edge: WfEdge, s: WfNode, t: WfNode): EdgeView {
    const ss = this.size(s);
    const ts = this.size(t);
    const scx = s.x + ss.w / 2;
    const scy = s.y + ss.h / 2;
    const tcx = t.x + ts.w / 2;
    const tcy = t.y + ts.h / 2;
    const base = { id: edge.id, label: edge.label, rejected: edge.label === 'Rejected' };

    if (Math.abs(tcy - scy) < 20 && t.x >= s.x + ss.w) {
      const sx = s.x + ss.w;
      return { ...base, d: `M ${sx} ${scy} L ${t.x - 2} ${tcy}`, lx: sx + 14, ly: scy - 8, anchor: 'middle' };
    }

    if (Math.abs(tcy - scy) < 20) {
      // Same row but pointing backwards: loop underneath
      const sy = s.y + ss.h;
      const low = Math.max(sy, t.y + ts.h) + 30;
      return { ...base, d: `M ${scx} ${sy} L ${scx} ${low} L ${tcx} ${low} L ${tcx} ${t.y + ts.h + 2}`, lx: scx - 8, ly: sy + 20, anchor: 'end' };
    }

    const down = tcy > scy;
    const sy = down ? s.y + ss.h : s.y;
    if (Math.abs(tcx - scx) < ts.w / 2) {
      const ty = down ? t.y - 2 : t.y + ts.h + 2;
      return { ...base, d: `M ${scx} ${sy} L ${scx} ${ty}`, lx: scx - 8, ly: sy + (down ? 20 : -12), anchor: 'end' };
    }
    const tx = tcx > scx ? t.x - 2 : t.x + ts.w + 2;
    return { ...base, d: `M ${scx} ${sy} L ${scx} ${tcy} L ${tx} ${tcy}`, lx: scx - 8, ly: sy + (down ? 20 : -12), anchor: 'end' };
  }

  // ===== Zoom =====

  zoomBy(step: number): void {
    this.zoom = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.round((this.zoom + step) * 10) / 10));
  }

  get zoomLabel(): string {
    return `${Math.round(this.zoom * 100)}%`;
  }

  /** Fit the whole graph in the visible area. */
  fit(): void {
    const el = this.viewport.nativeElement;
    let right = 0;
    let bottom = 0;
    for (const node of this.graph.nodes) {
      const s = this.size(node);
      right = Math.max(right, node.x + s.w + 30);
      bottom = Math.max(bottom, node.y + s.h + 30);
    }
    if (!right) return;
    const z = Math.min(el.clientWidth / right, el.clientHeight / bottom);
    this.zoom = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.floor(z * 10) / 10));
    el.scrollTo({ left: 0, top: 0 });
  }

  onScroll(): void {
    const el = this.viewport.nativeElement;
    this.scrollLeft = el.scrollLeft;
    this.scrollTop = el.scrollTop;
  }

  // ===== Minimap =====

  get miniScale(): number {
    const c = this.content;
    return Math.min(MINI_W / c.w, MINI_H / c.h);
  }

  /** Visible part of the canvas, in minimap units. */
  miniViewport(): { x: number; y: number; w: number; h: number } {
    const el = this.viewport.nativeElement;
    const k = this.miniScale / this.zoom;
    return {
      x: this.scrollLeft * k,
      y: this.scrollTop * k,
      w: Math.min(MINI_W, el.clientWidth * k),
      h: Math.min(MINI_H, el.clientHeight * k)
    };
  }

  /** Clicking the minimap centres the canvas on that point. */
  miniJump(event: MouseEvent): void {
    const target = event.currentTarget as SVGElement;
    const rect = target.getBoundingClientRect();
    const k = this.zoom / this.miniScale;
    const el = this.viewport.nativeElement;
    el.scrollTo({
      left: (event.clientX - rect.left) * k - el.clientWidth / 2,
      top: (event.clientY - rect.top) * k - el.clientHeight / 2
    });
  }

  // ===== Selection + drag =====

  startDrag(event: PointerEvent, node: WfNode): void {
    if (event.button !== 0) return;
    event.stopPropagation();
    this.drag = { node, startX: event.clientX, startY: event.clientY, origX: node.x, origY: node.y, moved: false };
  }

  @HostListener('document:pointermove', ['$event'])
  onPointerMove(event: PointerEvent): void {
    const d = this.drag;
    if (!d || this.readonly) return;
    const dx = (event.clientX - d.startX) / this.zoom;
    const dy = (event.clientY - d.startY) / this.zoom;
    if (!d.moved && Math.abs(dx) + Math.abs(dy) < 4) return;
    d.moved = true;
    d.node.x = Math.max(0, Math.round(d.origX + dx));
    d.node.y = Math.max(0, Math.round(d.origY + dy));
  }

  @HostListener('document:pointerup')
  onPointerUp(): void {
    const d = this.drag;
    if (!d) return;
    this.drag = null;
    if (d.moved) this.moved.emit();
    else this.select.emit(d.node.id);
  }

  /** Click on empty canvas clears the selection. */
  onSurfaceClick(event: MouseEvent): void {
    const el = event.target as HTMLElement;
    if (el.classList.contains('wf-surface')) this.select.emit(null);
  }

  @HostListener('document:keydown', ['$event'])
  onKey(event: KeyboardEvent): void {
    const tag = (event.target as HTMLElement).tagName;
    if (event.key === 'Delete' && this.selectedId && !['INPUT', 'TEXTAREA', 'SELECT'].includes(tag)) this.remove.emit();
  }

  toggleAdd(): void {
    this.addOpen = !this.addOpen;
  }

  addNode(type: NodeType): void {
    this.addOpen = false;
    this.add.emit(type);
  }
}
