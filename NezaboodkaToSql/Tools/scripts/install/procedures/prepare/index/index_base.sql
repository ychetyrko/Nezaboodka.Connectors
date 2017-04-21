CREATE TABLE `${db_name}`.`index_base` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL,
	`target_type_id` INT NOT NULL,
	`index_type` ENUM (
		'secondary',
		'referencial'
		) NOT NULL,

	-- referencial index
	`type_id` INT NOT NULL,
	`field_id` INT,

	-- secondary index
	`is_unique` BOOLEAN NOT NULL,

	FOREIGN KEY (`target_type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE,

	FOREIGN KEY (`field_id`)
		REFERENCES `field`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;
