import { Component, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { InputTextModule } from 'primeng/inputtext';
import { InfoCardComponent, SpiderlyButtonComponent } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';
import { FormsModule } from "@angular/forms";
import { ApiService } from 'src/app/business/services/api/api.service';
import { CommonModule } from '@angular/common';
import { Message } from 'src/app/business/entities/business-entities.generated';
import { MarkdownComponent } from 'ngx-markdown';
import { MessageRoleCodes } from 'src/app/business/enums/business-enums.generated';

@Component({
    templateUrl: './homepage.component.html',
    imports: [
    InfoCardComponent,
    TranslocoDirective,
    InputTextModule,
    FormsModule,
    SpiderlyButtonComponent,
    CommonModule,
    MarkdownComponent
],
})
export class HomepageComponent implements OnInit {
  agentMessageCode = MessageRoleCodes.Agent;
  companyName = this.config.companyName;

  prompt: string;
  chat: Message[] = [
    {role: 'user', content: 'test'},
    {role: 'agent', content: 'test'}
  ];

  constructor(
    private config: ConfigService,
    private apiService: ApiService
  ) {}

  ngOnInit() {

  }

  saveProductsToDb() {
    this.apiService.saveProductsToVectorDb().subscribe();
  }

  sendMessage = () => {
    const message: Message = {
      content: this.prompt, 
      role: MessageRoleCodes.Agent,
    };
    
    const body = {
      ...message, 
      chatHistory: [...this.chat]
    };
    
    this.chat.push(message);

    this.apiService.sendMessage(body).subscribe((response) => {
      this.chat.push({content: response, role: MessageRoleCodes.Agent});
    });
  }

  ngOnDestroy(): void {
  }

}