import { Component, OnInit } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { InputTextModule } from 'primeng/inputtext';
import { InfoCardComponent, SpiderlyButtonComponent } from 'spiderly';
import { ConfigService } from 'src/app/business/services/config.service';
import { FormsModule } from "@angular/forms";
import { ApiService } from 'src/app/business/services/api/api.service';
import { CommonModule } from '@angular/common';
import { Message } from 'src/app/business/entities/business-entities.generated';

@Component({
    templateUrl: './homepage.component.html',
    imports: [
    InfoCardComponent,
    TranslocoDirective,
    InputTextModule,
    FormsModule,
    SpiderlyButtonComponent,
    CommonModule,
],
})
export class HomepageComponent implements OnInit {
  companyName = this.config.companyName;

  prompt: string;
  chat: Message[] = [];

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
      role: 'user',
    };
    
    const body = {
      ...message, 
      chatHistory: [...this.chat]
    };
    
    this.chat.push(message);

    this.apiService.sendMessage(body).subscribe((response) => {
      this.chat.push({content: response, role: 'agent'});
    });
  }

  ngOnDestroy(): void {
  }

}